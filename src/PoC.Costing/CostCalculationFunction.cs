using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using PoC.Shared.Models;
using PoC.Shared.Validators;
using System.Text.Json;
using System.Net;

namespace PoC.Costing;

public class CostCalculationFunction
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly string _tableName = "costing-prices-table";
    private readonly CostCalculationRequestValidator _validator = new();

    public CostCalculationFunction()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{localStackHost}:{edgePort}";
        }

        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = "us-east-1"
        };
        _dynamoClient = new AmazonDynamoDBClient(config);
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                return CreateProblemDetails(HttpStatusCode.BadRequest, "Request body is required", "Empty request body");
            }

            var costRequest = JsonSerializer.Deserialize<CostCalculationRequest>(request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (costRequest == null)
            {
                return CreateProblemDetails(HttpStatusCode.BadRequest, "Invalid request format", "Failed to deserialize request");
            }

            // Validate using FluentValidation
            var validationResult = await _validator.ValidateAsync(costRequest);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return CreateProblemDetails(HttpStatusCode.BadRequest, "Validation failed", string.Join("; ", errors));
            }

            context.Logger.LogInformation($"Calculating cost for material: {costRequest.MaterialId}");

            // Fetch all component prices
            var table = Table.LoadTable(_dynamoClient, _tableName);
            var componentNames = costRequest.Formulation.Select(f => f.Component).Distinct().ToList();
            var prices = new Dictionary<string, (decimal UnitPrice, string Currency)>();
            var missingComponents = new List<string>();

            foreach (var componentName in componentNames)
            {
                var doc = await table.GetItemAsync(componentName);
                if (doc == null)
                {
                    missingComponents.Add(componentName);
                }
                else
                {
                    prices[componentName] = (
                        decimal.Parse(doc["UnitPrice"].AsString()),
                        doc["Currency"].AsString()
                    );
                }
            }

            // Business Rule: All components must have prices
            if (missingComponents.Any())
            {
                var detail = $"Missing prices for components: {string.Join(", ", missingComponents)}";
                return CreateProblemDetails(HttpStatusCode.BadRequest, "Missing component prices", detail);
            }

            // Verify all components use the same currency
            var currencies = prices.Values.Select(p => p.Currency).Distinct().ToList();
            if (currencies.Count > 1)
            {
                return CreateProblemDetails(HttpStatusCode.BadRequest, "Currency mismatch", 
                    $"All components must use the same currency. Found: {string.Join(", ", currencies)}");
            }

            var currency = currencies.First();

            // Calculate cost breakdown
            var breakdown = new List<CostBreakdownItem>();
            decimal totalCost = 0;

            foreach (var formulationItem in costRequest.Formulation)
            {
                var (unitPrice, _) = prices[formulationItem.Component];
                var contributionCost = unitPrice * (decimal)(formulationItem.Percentage / 100.0);
                
                breakdown.Add(new CostBreakdownItem(
                    formulationItem.Component,
                    formulationItem.Percentage,
                    unitPrice,
                    contributionCost
                ));

                totalCost += contributionCost;
            }

            // Calculate margin if requested
            MarginAnalysis? marginAnalysis = null;
            if (costRequest.DesiredMarginPercent.HasValue)
            {
                var marginPercent = costRequest.DesiredMarginPercent.Value;
                var suggestedSellingPrice = totalCost / (1 - marginPercent / 100);
                var estimatedGrossProfit = suggestedSellingPrice - totalCost;

                marginAnalysis = new MarginAnalysis(
                    marginPercent,
                    suggestedSellingPrice,
                    estimatedGrossProfit
                );
            }

            var response = new CostCalculationResponse(
                costRequest.MaterialId,
                totalCost,
                currency,
                breakdown,
                marginAnalysis
            );

            context.Logger.LogInformation($"Cost calculation complete. Total: {totalCost} {currency}");
            return CreateResponse(HttpStatusCode.OK, response);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error calculating cost: {ex.Message}");
            return CreateProblemDetails(HttpStatusCode.InternalServerError, "Internal server error", ex.Message);
        }
    }

    private APIGatewayHttpApiV2ProxyResponse CreateResponse(HttpStatusCode statusCode, object body)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Body = JsonSerializer.Serialize(body),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }

    private APIGatewayHttpApiV2ProxyResponse CreateProblemDetails(HttpStatusCode statusCode, string title, string detail)
    {
        var problem = new
        {
            type = "about:blank",
            title,
            status = (int)statusCode,
            detail
        };

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Body = JsonSerializer.Serialize(problem),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/problem+json" } }
        };
    }
}

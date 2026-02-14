using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using PoC.Shared.Models;
using PoC.Shared.Validators;
using System.Text.Json;
using System.Net;

namespace PoC.Costing;

public class PriceManagementFunction
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly string _tableName = "costing-prices-table";
    private readonly ComponentPriceRequestValidator _validator = new();

    public PriceManagementFunction()
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

            var priceRequest = JsonSerializer.Deserialize<ComponentPriceRequest>(request.Body, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (priceRequest == null)
            {
                return CreateProblemDetails(HttpStatusCode.BadRequest, "Invalid request format", "Failed to deserialize request");
            }

            // Validate using FluentValidation
            var validationResult = await _validator.ValidateAsync(priceRequest);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return CreateProblemDetails(HttpStatusCode.BadRequest, "Validation failed", string.Join("; ", errors));
            }

            context.Logger.LogInformation($"Upserting price for component: {priceRequest.ComponentName}");

            var updatedAt = DateTime.UtcNow;
            var table = Table.LoadTable(_dynamoClient, _tableName);
            
            var document = new Document
            {
                ["ComponentName"] = priceRequest.ComponentName,
                ["UnitPrice"] = priceRequest.UnitPrice,
                ["Unit"] = priceRequest.Unit,
                ["Currency"] = priceRequest.Currency,
                ["UpdatedAt"] = updatedAt.ToString("O")
            };

            await table.PutItemAsync(document);

            var response = new ComponentPriceResponse(
                priceRequest.ComponentName,
                priceRequest.UnitPrice,
                priceRequest.Unit,
                priceRequest.Currency,
                updatedAt
            );

            return CreateResponse(HttpStatusCode.OK, response);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error upserting price: {ex.Message}");
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

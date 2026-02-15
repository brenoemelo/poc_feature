using System.Net;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using FluentAssertions;
using PoC.E2E.Common;
using RestSharp;

namespace PoC.E2E.Tests;

public class CostingApiTests : ApiTestBase
{
    private readonly List<string> _createdComponents = new();

    public static IAmazonDynamoDB CreateDynamoClient()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            var host = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{host}:{port}";
        }

        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1"
        };
        var creds = new BasicAWSCredentials("test", "test");
        return new AmazonDynamoDBClient(creds, config);
    }

    [Fact]
    public async Task UpsertPrice_Should_Create_Price_And_Return_OkAsync()
    {
        var componentName = $"component-{Guid.NewGuid():N}";
        _createdComponents.Add(componentName);

        var requestBody = new ComponentPriceRequest(
            ComponentName: componentName,
            UnitPrice: 10.5m,
            Unit: "kg",
            Currency: "USD");

        var request = new RestRequest("/costing/prices", Method.Post)
            .AddJsonBody(requestBody);

        var response = await Client.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Depending on API response structure, we might parse it. 
        // The endpoint returns { message = "Price updated successfully" }
        response.Content.Should().Contain("Price updated successfully");
    }

    [Fact]
    public async Task CalculateCost_Should_Return_Correct_TotalAsync()
    {
        // 1. Create components with prices
        var componentA = $"comp-a-{Guid.NewGuid():N}";
        var componentB = $"comp-b-{Guid.NewGuid():N}";
        _createdComponents.Add(componentA);
        _createdComponents.Add(componentB);

        await UpsertPriceAsync(componentA, 10m);
        await UpsertPriceAsync(componentB, 20m);

        // 2. Calculate cost
        var materialId = $"mat-{Guid.NewGuid():N}";
        var calculationRequest = new CostCalculationRequest(
            MaterialId: materialId,
            Formulation: new List<FormulationInput>
            {
                new(Component: componentA, Percentage: 40), // 40% of 10 = 4
                new(Component: componentB, Percentage: 60) // 60% of 20 = 12
            },
            DesiredMarginPercent: 20);

        var request = new RestRequest("/costing/calculate-cost", Method.Post)
            .AddJsonBody(calculationRequest);

        var response = await Client.ExecuteAsync<CostCalculationResponse>(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Data.Should().NotBeNull();
        response.Data!.TotalCost.Should().Be(16m); // 4 + 12 = 16
        response.Data.Breakdown.Should().HaveCount(2);

        // Check margin
        response.Data.Margin.Should().NotBeNull();
        // 20% margin: Selling Price = Cost / (1 - Margin%) = 16 / 0.8 = 20
        response.Data.Margin!.SuggestedSellingPrice.Should().Be(20m);
    }

    [Fact]
    public async Task CalculateCost_With_Missing_Price_Should_FailAsync()
    {
        var componentMissing = $"missing-{Guid.NewGuid():N}";
        var materialId = $"mat-{Guid.NewGuid():N}";

        var calculationRequest = new CostCalculationRequest(
            MaterialId: materialId,
            Formulation: new List<FormulationInput>
            {
                new(Component: componentMissing, Percentage: 100)
            },
            DesiredMarginPercent: null);

        var request = new RestRequest("/costing/calculate-cost", Method.Post)
            .AddJsonBody(calculationRequest);

        var response = await Client.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Should().Contain("Missing component prices");
    }

    private async Task UpsertPriceAsync(string componentName, decimal price)
    {
        var requestBody = new ComponentPriceRequest(
            ComponentName: componentName,
            UnitPrice: price,
            Unit: "kg",
            Currency: "USD");

        var request = new RestRequest("/costing/prices", Method.Post)
            .AddJsonBody(requestBody);

        var response = await Client.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Records
    public sealed record ComponentPriceRequest(
        [property: JsonPropertyName("component_name")] string ComponentName,
        [property: JsonPropertyName("unit_price")] decimal UnitPrice,
        [property: JsonPropertyName("unit")] string Unit,
        [property: JsonPropertyName("currency")] string Currency
    );

    public sealed record CostCalculationRequest(
        [property: JsonPropertyName("material_id")] string MaterialId,
        [property: JsonPropertyName("formulation")] List<FormulationInput> Formulation,
        [property: JsonPropertyName("desired_margin_percent")] decimal? DesiredMarginPercent
    );

    public sealed record FormulationInput(
        [property: JsonPropertyName("component")] string Component,
        [property: JsonPropertyName("percentage")] double Percentage
    );

    public sealed record CostCalculationResponse(
        [property: JsonPropertyName("material_id")] string MaterialId,
        [property: JsonPropertyName("total_cost")] decimal TotalCost,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("breakdown")] List<CostBreakdownItem> Breakdown,
        [property: JsonPropertyName("margin")] MarginAnalysis? Margin
    );

    public sealed record CostBreakdownItem(
        [property: JsonPropertyName("component")] string Component,
        [property: JsonPropertyName("percentage")] double Percentage,
        [property: JsonPropertyName("unit_price")] decimal UnitPrice,
        [property: JsonPropertyName("contribution_cost")] decimal ContributionCost
    );

    public sealed record MarginAnalysis(
        [property: JsonPropertyName("desired_margin_percent")] decimal DesiredMarginPercent,
        [property: JsonPropertyName("suggested_selling_price")] decimal SuggestedSellingPrice,
        [property: JsonPropertyName("estimated_gross_profit")] decimal EstimatedGrossProfit
    );
}

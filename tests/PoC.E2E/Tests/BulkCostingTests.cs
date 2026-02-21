using System.Net;
using System.Text.Json.Serialization;
using FluentAssertions;
using PoC.E2E.Common;
using PoC.Shared.Common;
using RestSharp;

namespace PoC.E2E.Tests;

[Collection("E2E Tests")]
public class BulkCostingTests : ApiTestBase
{
    private readonly List<string> _createdComponents = new();
    private readonly List<string> _createdMaterials = new();

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Enable all required flags
        await FeatureManager.EnableFlagAsync("materials-crud");
        await FeatureManager.EnableFlagAsync("price-ingestion");
        await FeatureManager.EnableFlagAsync("costing-batch");
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    [Fact]
    public async Task CalculateAllCosts_Should_Return_Costs_For_All_MaterialsAsync()
    {
        // 1. Setup: Create components and prices
        var componentA = $"bulk-comp-a-{Guid.NewGuid():N}";
        var componentB = $"bulk-comp-b-{Guid.NewGuid():N}";
        _createdComponents.Add(componentA);
        _createdComponents.Add(componentB);

        await UpsertPriceAsync(componentA, 10m);
        await UpsertPriceAsync(componentB, 20m);

        // 2. Setup: Create multiple materials using these components
        // Material 1: 50% A, 50% B -> Cost = 5 + 10 = 15
        var material1Id = $"bulk-mat-1-{Guid.NewGuid():N}";
        await CreateMaterialAsync(material1Id, new[] 
        { 
            new { component = componentA, percentage = 50.0, type = "Polymer" },
            new { component = componentB, percentage = 50.0, type = "Reinforcement" }
        });
        _createdMaterials.Add(material1Id);

        // Material 2: 100% A -> Cost = 10
        var material2Id = $"bulk-mat-2-{Guid.NewGuid():N}";
        await CreateMaterialAsync(material2Id, new[] 
        { 
            new { component = componentA, percentage = 100.0, type = "Polymer" }
        });
        _createdMaterials.Add(material2Id);

        // 3. Wait for eventual consistency (DynamoDB + LocalStack)
        await Task.Delay(TimeSpan.FromSeconds(2));

        // 4. Act: Call Batch Endpoint
        var request = new RestRequest("/api/v1/costing/estimations/batch", Method.Get);
        var response = await Client.ExecuteAsync<ApiResponse<List<CostCalculationResponse>>>(request);

        // 4. Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Data.Should().NotBeNull();
        response.Data!.Data.Should().NotBeNull();
        response.Data.Data.Should().HaveCountGreaterThan(1);

        var calc1 = response.Data.Data.Find(c => c.MaterialId == material1Id);
        calc1.Should().NotBeNull();
        calc1!.TotalCost.Should().Be(15m);

        var calc2 = response.Data.Data.Find(c => c.MaterialId == material2Id);
        calc2.Should().NotBeNull();
        calc2!.TotalCost.Should().Be(10m);
    }

    private async Task UpsertPriceAsync(string componentName, decimal price)
    {
        var requestBody = new
        {
            component_name = componentName,
            unit_price = price,
            unit = "kg",
            currency = "USD"
        };

        var request = new RestRequest("/api/v1/costing/prices", Method.Post)
            .AddJsonBody(requestBody);

        var response = await Client.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task CreateMaterialAsync(string materialId, object formulation)
    {
        var requestBody = new
        {
            material_id = materialId,
            name = $"Test Material {materialId}",
            formulation = formulation,
            density = new { value = 1.0, unit = "g/cm3" },
            properties = new { }
        };
        
        var request = new RestRequest("/api/v1/materials", Method.Post)
            .AddJsonBody(requestBody);
        
        var response = await Client.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    public sealed record CostCalculationResponse(
        [property: JsonPropertyName("material_id")] string MaterialId,
        [property: JsonPropertyName("total_cost")] decimal TotalCost,
        [property: JsonPropertyName("currency")] string Currency
    );
}

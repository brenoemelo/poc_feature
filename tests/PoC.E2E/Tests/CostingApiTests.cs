using System.Net;
using FluentAssertions;
using PoC.E2E.Common;
using PoC.Shared.Common;
using PoC.Shared.Models;
using RestSharp;

namespace PoC.E2E.Tests;

[Collection("E2E Tests")]
public class CostingApiTests : ApiTestBase
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // 1. Enable Feature Flags
        await FeatureManager.EnableFlagAsync("price-ingestion");
        await FeatureManager.EnableFlagAsync("view-all-prices");
        await FeatureManager.EnableFlagAsync("count-all-prices");
        await FeatureManager.EnableFlagAsync("price-calculation");
        await FeatureManager.EnableFlagAsync("costing-batch");

        // 2. Wait for flag propagation (using Upsert Price endpoint as probe)
        // Sending a GET to /prices is safer/easier as a probe than POST
        await WaitForFlagAsync("/api/v1/costing/prices", Method.Get, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Costing_Lifecycle_Should_UpsertAndRetrievePricesAsync()
    {
        // Flags enabled in InitializeAsync
        // 1. Upsert Price
        var componentName = $"TestComponent-{Guid.NewGuid():N}";
        var upsertRequest = new RestRequest("/api/v1/costing/prices", Method.Post);
        upsertRequest.AddJsonBody(new ComponentPriceRequest(
            ComponentName: componentName,
            UnitPrice: 10.5m,
            Unit: "kg",
            Currency: "USD"));

        var upsertResponse = await Client.ExecuteAsync(upsertRequest);
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: $"upserting price for {componentName} should succeed. Content: {upsertResponse.Content}");

        // 2. Verify Price (Get)
        var getPricesRequest = new RestRequest("/api/v1/costing/prices", Method.Get);
        getPricesRequest.AddQueryParameter("names", componentName);

        var getResponse = await Client.ExecuteAsync<ApiResponse<List<ComponentPriceResponse>>>(getPricesRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Deserialize response
        var prices = getResponse.Data;
        prices.Should().NotBeNull();
        prices!.Data.Should().Contain(p => p.ComponentName == componentName && p.UnitPrice == 10.5m);

        // 3. Calculate Cost
        var materialId = "mat-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var calculateRequest = new RestRequest("/api/v1/costing/estimations", Method.Post);
        calculateRequest.AddJsonBody(new CostCalculationRequest(
            MaterialId: materialId,
            Formulation: new List<FormulationInput> 
            { 
                new FormulationInput(Component: componentName, Percentage: 100) 
            },
            DesiredMarginPercent: 20m));

        var calculateResponse = await Client.ExecuteAsync<ApiResponse<CostCalculationResponse>>(calculateRequest);
        calculateResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: "calculation should succeed");

        var calculation = calculateResponse.Data;
        calculation.Should().NotBeNull();
        calculation!.Data.TotalCost.Should().Be(10.5m); // 100% of 10.5
    }

    [Fact]
    public async Task Calculate_WithMissingPrice_Should_Return_ErrorAsync()
    {
        // Flags enabled in InitializeAsync
        var materialId = $"mat-missing-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        var missingComponent = $"MissingComponent-{Guid.NewGuid():N}";

        var calculateRequest = new RestRequest("/api/v1/costing/estimations", Method.Post);
        calculateRequest.AddJsonBody(new CostCalculationRequest(
            MaterialId: materialId,
            Formulation: new List<FormulationInput> 
            { 
                new FormulationInput(Component: missingComponent, Percentage: 100) 
            },
            DesiredMarginPercent: 20m));

        var calculateResponse = await Client.ExecuteAsync(calculateRequest);
        
        // It should be BadRequest (400) or NotFound (404) depending on implementation.
        // Based on "MissingPrices" error, it's likely treated as a validation/business rule failure -> 400.
        calculateResponse.StatusCode.Should().Match(
            s => s == HttpStatusCode.BadRequest || s == HttpStatusCode.NotFound,
            because: "calculating cost for missing component should fail");

        calculateResponse.Content.Should().Contain("Missing component prices");
    }
}

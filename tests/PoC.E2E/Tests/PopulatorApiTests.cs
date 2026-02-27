using System.Net;
using System.Text.Json;
using FluentAssertions;
using PoC.E2E.Common;
using PoC.Shared.Common;
using PoC.Shared.Models;
using RestSharp;

namespace PoC.E2E.Tests;

[Collection("E2E Tests")]
public class PopulatorApiTests : ApiTestBase
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // 1. Enable Feature Flags
        await FeatureManager.EnableFlagAsync("population-jobs");
        await FeatureManager.EnableFlagAsync("materials-crud");
        await FeatureManager.EnableFlagAsync("price-ingestion");
        
        // 2. Wait for flag propagation
        // We poll the endpoint until it stops returning 404/403.
        // Sending an empty body should result in 400 BadRequest if the service is up and flag is enabled.
        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(new { }); 
        
        for (int i = 0; i < 60; i++)
        {
            var response = await Client.ExecuteAsync(request);
            if (response.StatusCode != HttpStatusCode.NotFound && response.StatusCode != HttpStatusCode.Forbidden)
            {
                return;
            }

            await Task.Delay(1000);
        }
    }

    [Fact]
    public async Task Create_Population_Job_Should_Return_AcceptedAsync()
    {
        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(new PopulationRequest(Target: "materials", Count: 10));

        var response = await Client.ExecuteAsync(request);

        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, because: "population job should be accepted. Content: " + response.Content);
        response.Headers.Should().Contain(h => h.Name == "Location");
    }

    [Fact]
    public async Task Create_Population_Job_With_Custom_Range_Should_Be_AcceptedAsync()
    {
        var requestBody = new PopulationRequest(
            Target: "materials",
            Count: 5,
            MinComponents: 1,
            MaxComponents: 5);

        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(requestBody);

        var response = await Client.ExecuteAsync(request);
        
        response.IsSuccessful.Should().BeTrue();
        response.Content.Should().Contain("Population job accepted");
    }

    [Fact]
    public async Task Create_Population_Job_With_Invalid_Min_Components_Should_FailAsync()
    {
        var requestBody = new PopulationRequest(
            Target: "materials",
            Count: 5,
            MinComponents: 0, // Invalid: must be >= 1
            MaxComponents: 5);

        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(requestBody);

        var response = await Client.ExecuteAsync(request);

        response.IsSuccessful.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Population_Job_With_Max_Less_Than_Min_Components_Should_FailAsync()
    {
        var requestBody = new PopulationRequest(
            Target: "materials",
            Count: 5,
            MinComponents: 10,
            MaxComponents: 5); // Invalid: must be >= MinComponents

        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(requestBody);

        var response = await Client.ExecuteAsync(request);

        response.IsSuccessful.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Population_Job_Should_Increase_Material_CountAsync()
    {
        // 1. Get Initial Count
        var initialCount = await GetMaterialCountAsync();

        // 2. Create Job
        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(new PopulationRequest(Target: "materials", Count: 5));

        var response = await Client.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // 3. Poll for Count Increase
        int finalCount = initialCount;
        bool increased = false;
        
        // Give it up to 60 seconds for the worker to process
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(2000);
            finalCount = await GetMaterialCountAsync();
            if (finalCount >= initialCount + 5)
            {
                increased = true;
                break;
            }
        }

        increased.Should().BeTrue(because: $"material count should increase by at least 5. Initial: {initialCount}, Final: {finalCount}");
    }

    [Fact]
    public async Task Create_Ensure_Prices_Job_Should_Increase_Price_CountAsync()
    {
        // 1. Ensure we have materials with unique components
        var uniqueSuffix = Guid.NewGuid().ToString().Substring(0, 8);
        var material = new MaterialFormulation(
             $"MAT-ENSURE-{uniqueSuffix}",
             "Ensure Prices Material",
             new Density(1.0, "g/cm3"),
             new List<FormulationComponent> 
             { 
                 new FormulationComponent($"UniqueComp1-{uniqueSuffix}", 50, "Polymer"),
                 new FormulationComponent($"UniqueComp2-{uniqueSuffix}", 50, "Filler")
             },
             new Dictionary<string, string>(),
             null);
        
        var createResponse = await Client.ExecuteAsync(new RestRequest("/api/v1/materials", Method.Post).AddJsonBody(material));
        // Retry logic for materials creation if needed (though InitializeAsync checks populator, materials might lag slightly?)
        // Assuming InitializeAsync check is enough for flags, but let's be safe.
        if (createResponse.StatusCode == HttpStatusCode.NotFound)
        {
            await Task.Delay(2000);
            createResponse = await Client.ExecuteAsync(new RestRequest("/api/v1/materials", Method.Post).AddJsonBody(material));
        }

        createResponse.IsSuccessful.Should().BeTrue();

        // 2. Get initial price count
        var initialCount = await GetPriceCountAsync();
        Console.WriteLine($"Initial Price Count: {initialCount}");

        // 3. Trigger ensure-prices job repeatedly until count increases
        // The GSI is eventually consistent, so we might need to retry the job trigger.
        int maxRetries = 40; 
        bool pricesIncreased = false;
        int expectedMinCount = initialCount + 2;

        for (int i = 0; i < maxRetries; i++)
        {
            // Trigger job
            var requestBody = new PopulationRequest(Target: "ensure-prices", Count: 1);
            var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
            request.AddJsonBody(requestBody);
            
            var popResponse = await Client.ExecuteAsync(request);
            popResponse.IsSuccessful.Should().BeTrue();

            // Wait for processing
            await Task.Delay(3000);

            // Check count
            var currentCount = await GetPriceCountAsync();
            Console.WriteLine($"Retry {i}: Price Count = {currentCount} (Expected >= {expectedMinCount})");
            
            if (currentCount >= expectedMinCount)
            {
                pricesIncreased = true;
                break;
            }
        }

        pricesIncreased.Should().BeTrue("Prices count should increase after ensure-prices job");
    }

    private async Task<int> GetMaterialCountAsync()
    {
        var request = new RestRequest("/api/v1/materials/count", Method.Get);
        var response = await Client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            // If failing, return -1 or throw. Throwing is better for tests.
             return 0; 
        }
        
        try 
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<MaterialCountResponse>>(
                response.Content!, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return apiResponse?.Data?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<int> GetPriceCountAsync()
    {
        var request = new RestRequest("/api/v1/costing/prices/count", Method.Get);
        var response = await Client.ExecuteAsync(request);

        if (!response.IsSuccessful) return 0;
        
        try 
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<MaterialCountResponse>>(
                response.Content!, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return apiResponse?.Data?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    // Helper record for deserialization
    private record MaterialCountResponse(int Count);
}

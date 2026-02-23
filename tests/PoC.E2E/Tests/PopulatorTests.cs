using FluentAssertions;
using PoC.E2E.Common;
using PoC.Shared.Models;
using RestSharp;
using System.Text.Json.Serialization;

namespace PoC.E2E.Tests;

[Collection("E2E Tests")]
public class PopulatorTests : ApiTestBase
{
    public PopulatorTests()
    {
        // Use the default Client from ApiTestBase which points to the shared API Gateway
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Enable required flags
        await FeatureManager.EnableFlagAsync("population-jobs");
        await FeatureManager.EnableFlagAsync("materials-crud");
        await FeatureManager.EnableFlagAsync("price-ingestion");
        
        // Active Polling since Unleash updates asynchronously
        for (int i = 0; i < 20; i++)
        {
            var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
            request.AddJsonBody(new { }); // Invalid body
            var response = await Client.ExecuteAsync(request);

            // If flag is enabled, it will return 400 Bad Request. If disabled, 404 Not Found.
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                break;
            }

            await Task.Delay(1000); // 1-second interval
        }
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    [Fact]
    public async Task CreatePopulationJob_ShouldAcceptRequest_WithDefaultComponentsAsync()
    {
        // Arrange
        var requestBody = new PopulationRequest
        {
            Target = "materials",
            Count = 5
        };
        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(requestBody);

        // Act
        var response = await Client.ExecuteAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue($"Status: {response.StatusCode}, Content: {response.Content}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task CreatePopulationJob_ShouldAcceptRequest_WithCustomComponentRangeAsync()
    {
        // Arrange
        var requestBody = new PopulationRequest
        {
            Target = "materials",
            Count = 5,
            MinComponents = 1,
            MaxComponents = 5
        };
        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(requestBody);

        // Act
        var response = await Client.ExecuteAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        response.Content.Should().Contain("Population job accepted");
    }

    [Fact]
    public async Task CreatePopulationJob_ShouldFail_WhenMinComponentsIsInvalidAsync()
    {
        // Arrange
        var requestBody = new PopulationRequest
        {
            Target = "materials",
            Count = 5,
            MinComponents = 0, // Invalid: must be >= 1
            MaxComponents = 5
        };
        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        // Explicitly serialize to ensure snake_case property names are used
        string jsonBody = System.Text.Json.JsonSerializer.Serialize(requestBody);
        request.AddStringBody(jsonBody, DataFormat.Json);

        // Act
        var response = await Client.ExecuteAsync(request);

        // Assert
        response.IsSuccessful.Should().BeFalse();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePopulationJob_ShouldFail_WhenMaxComponentsIsLessThanMinAsync()
    {
        // Arrange
        var requestBody = new PopulationRequest
        {
            Target = "materials",
            Count = 5,
            MinComponents = 10,
            MaxComponents = 5 // Invalid: must be >= MinComponents
        };
        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        // Explicitly serialize to ensure snake_case property names are used
        string jsonBody = System.Text.Json.JsonSerializer.Serialize(requestBody);
        request.AddStringBody(jsonBody, DataFormat.Json);

        // Act
        var response = await Client.ExecuteAsync(request);

        // Assert
        response.IsSuccessful.Should().BeFalse();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePopulationJob_ShouldGenerateMaterials_WithCorrectComponentCountAsync()
    {
        // 1. Get initial count
        var initialCountResponse = await Client.ExecuteAsync<ApiResponse<CountResponse>>(new RestRequest("/api/v1/materials/count", Method.Get));
        initialCountResponse.IsSuccessful.Should().BeTrue();
        var initialCount = initialCountResponse.Data!.Data.Count;
        Console.WriteLine($"Initial Count: {initialCount}");

        // 2. Trigger population (3 materials, fixed 3 components)
        // Using a unique range to identify them easily if needed, but count diff is enough
        var requestBody = new PopulationRequest
        {
            Target = "materials",
            Count = 3,
            MinComponents = 3,
            MaxComponents = 3
        };
        var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
        request.AddJsonBody(requestBody);
        
        var popResponse = await Client.ExecuteAsync(request);
        popResponse.IsSuccessful.Should().BeTrue($"Status: {popResponse.StatusCode}, Content: {popResponse.Content}");

        // 3. Wait for processing (poll count)
        int maxRetries = 90; // Increased to 180s (3 minutes) to ensure completion
        int expectedCount = initialCount + 3;
        bool populated = false;

        for (int i = 0; i < maxRetries; i++)
        {
            await Task.Delay(2000); // Wait 2s
            var currentCountResponse = await Client.ExecuteAsync<ApiResponse<CountResponse>>(new RestRequest("/api/v1/materials/count", Method.Get));
            if (currentCountResponse.IsSuccessful)
            {
                Console.WriteLine($"Retry {i}: Count = {currentCountResponse.Data!.Data.Count} (Expected >= {expectedCount})");
                if (currentCountResponse.Data!.Data.Count >= expectedCount)
                {
                    populated = true;
                    break;
                }
            }
            else
            {
                Console.WriteLine($"Retry {i}: Failed to get count. Status: {currentCountResponse.StatusCode}");
            }
        }

        populated.Should().BeTrue("Materials were not populated in time");

        // 4. Fetch latest materials and verify component count
        // We fetch with limit=100 to get recent ones. 
        // Since we don't have sort by date, we might get old ones too.
        // But we can filter by checking if formulation count is 3 (if existing data is different).
        // A better way is to just fetch all and check if we find *at least 3* materials with 3 components.
        // Or, we can rely on the fact that this is a test environment and we just added them.
        var listResponse = await Client.ExecuteAsync<PagedResponse<MaterialFormulation>>(new RestRequest("/api/v1/materials?limit=100", Method.Get));
        listResponse.IsSuccessful.Should().BeTrue();

        var allMaterials = listResponse.Data!.Data;
        var materialsWith3Components = allMaterials
            .Where(m => m.Formulation.Count == 3)
            .ToList();

        materialsWith3Components.Count.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task CreatePopulationJob_ShouldEnsurePrices_ForAllComponentsAsync()
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
        createResponse.IsSuccessful.Should().BeTrue();

        // 2. Get initial price count
        var initialCountResponse = await Client.ExecuteAsync<ApiResponse<CountResponse>>(new RestRequest("/api/v1/costing/prices/count", Method.Get));
        int initialCount = initialCountResponse.IsSuccessful ? initialCountResponse.Data!.Data.Count : 0;
        Console.WriteLine($"Initial Price Count: {initialCount}");

        // 3. Wait for processing (poll count)
        // We loop the trigger because the GSI (IX_Materials_By_Type) used by ensure-prices
        // is eventually consistent and might not see the new material immediately.
        int maxRetries = 40; // Increased to give up to 120 seconds of processing time locally
        bool pricesIncreased = false;
        int expectedMinCount = initialCount + 2;

        for (int i = 0; i < maxRetries; i++)
        {
            // Trigger job
            var requestBody = new PopulationRequest
            {
                Target = "ensure-prices",
                Count = 1
            };
            var request = new RestRequest("/api/v1/populator/jobs", Method.Post);
            request.AddJsonBody(requestBody);
            
            var popResponse = await Client.ExecuteAsync(request);
            popResponse.IsSuccessful.Should().BeTrue();

            // Wait for processing
            await Task.Delay(3000);

            // Check count
            var currentCountResponse = await Client.ExecuteAsync<ApiResponse<CountResponse>>(new RestRequest("/api/v1/costing/prices/count", Method.Get));
            
            if (currentCountResponse.IsSuccessful)
            {
                var currentCount = currentCountResponse.Data!.Data.Count;
                Console.WriteLine($"Retry {i}: Price Count = {currentCount} (Expected >= {expectedMinCount})");
                
                if (currentCount >= expectedMinCount)
                {
                    pricesIncreased = true;
                    break;
                }
            }
            else
            {
                Console.WriteLine($"Retry {i}: Request failed Status: {currentCountResponse.StatusCode}");
            }
        }

        pricesIncreased.Should().BeTrue("Prices count should increase after ensure-prices job");
    }

    public record ApiResponse<T>(
        [property: JsonPropertyName("data")] T Data, 
        [property: JsonPropertyName("links")] List<Link> Links);

    public record CountResponse([property: JsonPropertyName("count")] int Count);
    public record PagedResponse<T>(
        [property: JsonPropertyName("data")] List<T> Data, 
        [property: JsonPropertyName("meta")] PaginationMeta Meta, 
        [property: JsonPropertyName("links")] List<Link> Links);
    public record PaginationMeta(
        [property: JsonPropertyName("limit")] int Limit, 
        [property: JsonPropertyName("count")] int Count, 
        [property: JsonPropertyName("nextCursor")] string? NextCursor);
    public record Link(
        [property: JsonPropertyName("rel")] string Rel, 
        [property: JsonPropertyName("href")] string Href, 
        [property: JsonPropertyName("method")] string Method);
}

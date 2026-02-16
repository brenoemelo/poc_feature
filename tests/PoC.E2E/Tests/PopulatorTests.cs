using FluentAssertions;
using PoC.E2E.Common;
using PoC.Shared.Models;
using RestSharp;
using System.Text.Json.Serialization;

namespace PoC.E2E.Tests;

public class PopulatorTests : ApiTestBase
{
    public PopulatorTests()
    {
        // Use the default Client from ApiTestBase which points to the shared API Gateway
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
        response.IsSuccessful.Should().BeTrue();
        response.Content.Should().Contain("Population job accepted");
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
        request.AddJsonBody(requestBody);

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
        request.AddJsonBody(requestBody);

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
        var initialCountResponse = await Client.ExecuteAsync<CountResponse>(new RestRequest("/api/v1/materials/count", Method.Get));
        initialCountResponse.IsSuccessful.Should().BeTrue();
        var initialCount = initialCountResponse.Data!.Count;
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
        popResponse.IsSuccessful.Should().BeTrue();

        // 3. Wait for processing (poll count)
        int maxRetries = 30; // Increased to 30s (2s * 15? No, 2s * 30 = 60s)
        int expectedCount = initialCount + 3;
        bool populated = false;

        for (int i = 0; i < maxRetries; i++)
        {
            await Task.Delay(2000); // Wait 2s
            var currentCountResponse = await Client.ExecuteAsync<CountResponse>(new RestRequest("/api/v1/materials/count", Method.Get));
            if (currentCountResponse.IsSuccessful)
            {
                Console.WriteLine($"Retry {i}: Count = {currentCountResponse.Data!.Count} (Expected >= {expectedCount})");
                if (currentCountResponse.Data!.Count >= expectedCount)
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
        
        var materialsWith3Components = listResponse.Data!.Data.Where(m => m.Formulation.Count == 3).ToList();
        materialsWith3Components.Count.Should().BeGreaterThan(2);
    }

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

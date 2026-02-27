using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using FluentAssertions;
using PoC.E2E.Common;
using PoC.Shared.Common;
using PoC.Shared.Models;
using RestSharp;

namespace PoC.E2E.Tests;

[Collection("E2E Tests")]
public class MaterialsApiTests : ApiTestBase, IAsyncLifetime
{
    private readonly List<string> _createdIds = new();

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

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // 1. Enable Feature Flags
        await FeatureManager.EnableFlagAsync("materials-crud");
        await FeatureManager.EnableFlagAsync("view-all-components");
        
        // 2. Wait for flag propagation
        // Use GET /api/v1/materials?limit=1 as probe
        await WaitForFlagAsync("/api/v1/materials?limit=1", Method.Get, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Materials_Lifecycle_HappyPath_Should_CreateQueryDeleteAndReturn404Async()
    {
        Console.WriteLine("[Test] Starting Materials_Lifecycle_HappyPath");
        // Flags enabled in InitializeAsync
        var materialId = $"e2e-{Guid.NewGuid():N}";
        
        // Create Material via API
        var material = new MaterialFormulation(
            MaterialId: materialId,
            Name: "E2E Test Material",
            Density: new Density(1.2, "g/cm3"),
            Formulation: new List<FormulationComponent>
            {
                new FormulationComponent("Polycarbonate", 60, "Base Polymer"),
                new FormulationComponent("ABS", 40, "Impact Modifier")
            },
            Properties: new Dictionary<string, string>
            {
                ["tensile_strength"] = "120 MPa",
                ["melting_point"] = "250C"
            },
            Version: null);

        Console.WriteLine($"[Test] Creating material {materialId}...");
        var createRequest = new RestRequest("/api/v1/materials", Method.Post);
        createRequest.AddJsonBody(material);
        
        var createResponse = await Client.ExecuteAsync<ApiResponse<MaterialFormulation>>(createRequest);
        Console.WriteLine($"[Test] Create Status: {createResponse.StatusCode}");
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, because: $"creation should succeed. Content: {createResponse.Content}");
        _createdIds.Add(materialId);

        Console.WriteLine("[Test] Listing materials...");
        var listRequest = new RestRequest("/api/v1/materials?limit=100", Method.Get);
        var listResponse = await Client.ExecuteAsync<PagedResponse<MaterialFormulation>>(listRequest);
        Console.WriteLine($"[Test] List Status: {listResponse.StatusCode}");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: $"listing materials should succeed. Content: {listResponse.Content}");
        listResponse.Data.Should().NotBeNull();
        listResponse.Data!.Data.Should().NotBeNull();
        listResponse.Data.Data.Should().NotBeEmpty(because: "listing should return at least some materials");

        Console.WriteLine("[Test] Getting material...");
        var getRequest = new RestRequest($"/api/v1/materials/{materialId}", Method.Get);
        var getResponse = await Client.ExecuteAsync<ApiResponse<MaterialFormulation>>(getRequest);
        Console.WriteLine($"[Test] Get Status: {getResponse.StatusCode}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: $"material {materialId} must exist. Content: {getResponse.Content}");
        getResponse.Data.Should().NotBeNull();
        getResponse.Data!.Data.Should().NotBeNull();
        getResponse.Data!.Data.MaterialId.Should().Be(materialId);

        Console.WriteLine("[Test] Deleting material...");
        var deleteRequest = new RestRequest($"/api/v1/materials/{materialId}", Method.Delete);
        var deleteResponse = await Client.ExecuteAsync(deleteRequest);
        Console.WriteLine($"[Test] Delete Status: {deleteResponse.StatusCode}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, because: "successful deletion should return 204");

        Console.WriteLine("[Test] Verifying 404 after delete...");
        var getAfterDeleteRequest = new RestRequest($"/api/v1/materials/{materialId}", Method.Get);
        var getAfterDeleteResponse = await Client.ExecuteAsync(getAfterDeleteRequest);
        Console.WriteLine($"[Test] Get After Delete Status: {getAfterDeleteResponse.StatusCode}");
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        Console.WriteLine("[Test] Finished successfully");
    }

    [Fact]
    public async Task Get_NonExistent_Should_Return_404_And_Not_200_With_ErrorBodyAsync()
    {
        var id = $"missing-{Guid.NewGuid():N}";
        var request = new RestRequest($"/api/v1/materials/{id}", Method.Get);
        var response = await Client.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Should().NotBeNullOrWhiteSpace();
        response.StatusCode.Should().NotBe(HttpStatusCode.OK, because: "must not return 200 for missing resource");
        response.Content!.Contains("Resource not found").Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonExistent_Should_Return_404_And_Not_204Async()
    {
        var id = $"missing-{Guid.NewGuid():N}";
        var request = new RestRequest($"/api/v1/materials/{id}", Method.Delete);
        var response = await Client.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, because: "deleting a missing resource should return 404");
        response.StatusCode.Should().NotBe(HttpStatusCode.NoContent, because: "must not return 204 when nothing is deleted");
    }

    [Fact]
    public async Task Get_Materials_WhenFlagDisabled_Should_Return_404Async()
    {
        // Arrange
        await FeatureManager.DisableFlagAsync("materials-crud");
        
        // Act & Assert (Active Polling since Unleash updates asynchronously)
        await WaitForFlagAsync("/api/v1/materials", Method.Get, HttpStatusCode.NotFound);

        // Verify explicitly for S2699
        var request = new RestRequest("/api/v1/materials", Method.Get);
        var response = await Client.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, because: "endpoint should be disabled when flag is off");
    }

    public override async Task DisposeAsync()
    {
        foreach (var id in _createdIds)
        {
            await DeleteDirectAsync(id);
        }

        await base.DisposeAsync();
    }

    private async Task DeleteDirectAsync(string materialId)
    {
        using var client = CreateDynamoClient();
        var request = new DeleteItemRequest
        {
            TableName = "materials-table",
            Key = new Dictionary<string, AttributeValue>
            {
                ["material_id"] = new AttributeValue { S = materialId }
            }
        };
        await client.DeleteItemAsync(request);
    }
}

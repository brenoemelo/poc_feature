using System.Net;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using FluentAssertions;
using PoC.E2E.Common;
using RestSharp;

namespace PoC.E2E.Tests;

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

    [Fact]
    public async Task Materials_Lifecycle_HappyPath_Should_CreateQueryDeleteAndReturn404Async()
    {
        var materialId = $"e2e-{Guid.NewGuid():N}";
        await InsertMaterialDirectlyAsync(materialId, "E2E Test Material");
        _createdIds.Add(materialId);

        var listRequest = new RestRequest("/materials", Method.Get);
        var listResponse = await Client.ExecuteAsync<List<MaterialResponse>>(listRequest);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: $"listing materials should succeed. Content: {listResponse.Content}");
        listResponse.Data.Should().NotBeNull();
        listResponse.Data!.Exists(m => m.MaterialId == materialId).Should().BeTrue(because: "inserted material must be present in the list");

        var getRequest = new RestRequest($"/materials/{materialId}", Method.Get);
        var getResponse = await Client.ExecuteAsync<MaterialResponse>(getRequest);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: $"material {materialId} must exist. Content: {getResponse.Content}");
        getResponse.Data.Should().NotBeNull();
        getResponse.Data!.MaterialId.Should().Be(materialId);

        var deleteRequest = new RestRequest($"/materials/{materialId}", Method.Delete);
        var deleteResponse = await Client.ExecuteAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, because: "successful deletion should return 204");

        var getAfterDeleteRequest = new RestRequest($"/materials/{materialId}", Method.Get);
        var getAfterDeleteResponse = await Client.ExecuteAsync(getAfterDeleteRequest);
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound, because: "deleted material should not be found");
    }

    [Fact]
    public async Task Get_NonExistent_Should_Return_404_And_Not_200_With_ErrorBodyAsync()
    {
        var id = $"missing-{Guid.NewGuid():N}";
        var request = new RestRequest($"/materials/{id}", Method.Get);
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
        var request = new RestRequest($"/materials/{id}", Method.Delete);
        var response = await Client.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, because: "deleting a missing resource should return 404");
        response.StatusCode.Should().NotBe(HttpStatusCode.NoContent, because: "must not return 204 when nothing is deleted");
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        foreach (var id in _createdIds)
        {
            await DeleteDirectAsync(id);
        }
    }

    private async Task InsertMaterialDirectlyAsync(string materialId, string name)
    {
        using var client = CreateDynamoClient();
        var request = new PutItemRequest
        {
            TableName = "materials-table",
            Item = new Dictionary<string, AttributeValue>
            {
                ["material_id"] = new AttributeValue
                {
                    S = materialId
                },
                ["name"] = new AttributeValue
                {
                    S = name
                },
                ["formulation"] = new AttributeValue
                {
                    L = new List<AttributeValue>
                    {
                        new AttributeValue
                        {
                            M = new Dictionary<string, AttributeValue>
                            {
                                ["component"] = new AttributeValue { S = "Polycarbonate" },
                                ["percentage"] = new AttributeValue { N = "60" },
                                ["type"] = new AttributeValue { S = "Base Polymer" }
                            }
                        }
                    }
                },
                ["properties"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["tensile_strength"] = new AttributeValue { S = "120 MPa" },
                        ["melting_point"] = new AttributeValue { S = "250C" }
                    }
                }
            }
        };
        await client.PutItemAsync(request);
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

    public record MaterialResponse(
        [property: JsonPropertyName("material_id")] string MaterialId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("properties")] Dictionary<string, string>? Properties);
}

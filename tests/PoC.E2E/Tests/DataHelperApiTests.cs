using System.Net;
using FluentAssertions;
using PoC.E2E.Common;
using RestSharp;
using System.Text.Json;

namespace PoC.E2E.Tests;

[Collection("E2E Tests")]
public class DataHelperApiTests : ApiTestBase
{
    [Fact]
    public async Task Get_Version_Should_Return_InformationalVersionAsync()
    {
        var request = new RestRequest("/api/v1/datahelper/version", Method.Get);
        var response = await Client.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(response.Content!);
        var root = doc.RootElement;
        root.TryGetProperty("version", out var versionProp).Should().BeTrue("response should include 'version'");
        var version = versionProp.GetString();
        version.Should().NotBeNullOrWhiteSpace();
        version!.StartsWith("Build-").Should().BeTrue("InformationalVersion should start with 'Build-'");
    }
}

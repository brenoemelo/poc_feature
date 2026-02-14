using System.Net;
using Antigravity.E2E.Common;
using FluentAssertions;
using RestSharp;
using Xunit;

namespace Antigravity.E2E.Tests;

public class ItemsApiTests : ApiTestBase
{
    [Fact]
    public async Task GetItems_ShouldReturn200_And_ListOfItems()
    {
        // Arrange
        var request = new RestRequest("/api/v1/items", Method.Get);

        // Act
        var response = await Client.ExecuteAsync<List<ItemResponse>>(request);

        // Assert
        // 1. Valida Status Code
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            because: $"the endpoint /items needs to be accessible. Content: {response.Content}");

        // 2. Valida Dados
        response.Data.Should().NotBeNull();
        
        // Exemplo: se espera que a lista não esteja vazia (depende do seu cenário de dados)
        // response.Data.Should().NotBeEmpty(); 
    }

    // Record local para mapear a resposta (apenas o que importa para o teste)
    public record ItemResponse(Guid Id, string Name, decimal Weight);
}
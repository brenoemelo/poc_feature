using Bogus;
using PoC.Shared.Common;
using PoC.Shared.Models;
using System.Text.Json;

namespace PoC.Populator.Domain.Services;

public class PopulationContext
{
    public HttpClient HttpClient { get; set; } = null!;
    public Action<string, Exception?>? LogError { get; set; }
}

public interface IPopulationStrategy
{
    string TargetTable { get; }
    Task<IEnumerable<object>> GenerateAsync(int count, PopulationContext context, int? minComponents = null, int? maxComponents = null);
}

public sealed class MaterialPopulationStrategy : IPopulationStrategy
{
    public string TargetTable => "materials-table";

    public Task<IEnumerable<object>> GenerateAsync(int count, PopulationContext context, int? minComponents = null, int? maxComponents = null)
    {
        int min = minComponents ?? 20;
        int max = maxComponents ?? 20;
        if (max < min) max = min;

        var componentFaker = new Faker<FormulationComponent>()
            .CustomInstantiator(f => new FormulationComponent(
                Component: f.Commerce.ProductMaterial(),
                Percentage: Math.Round(f.Random.Double(1, 100), 2),
                Type: f.PickRandom(new[] { "Base Polymer", "Additive", "Reinforcement", "Filler" })));

        var faker = new Faker<MaterialFormulation>()
            .CustomInstantiator(f => new MaterialFormulation(
                MaterialId: $"MAT-{f.Random.Guid().ToString().Substring(0, 8).ToUpper()}",
                Name: f.Commerce.ProductName(),
                Density: new Density(Math.Round(f.Random.Double(0.8, 3.0), 2), "g/cm3"),
                Formulation: componentFaker.Generate(f.Random.Int(min, max)),
                Properties: new Dictionary<string, string>
                {
                    { "tensile_strength", $"{f.Random.Int(50, 200)} MPa" },
                    { "melting_point", $"{f.Random.Int(150, 400)}C" },
                    { "color", f.Commerce.Color() }
                },
                Version: null));

        return Task.FromResult(faker.Generate(count).Cast<object>());
    }
}

public sealed class PricePopulationStrategy : IPopulationStrategy
{
    public string TargetTable => "prices-table";
    private readonly Faker<ComponentPriceRequest> _faker;

    public PricePopulationStrategy()
    {
         _faker = new Faker<ComponentPriceRequest>()
             .CustomInstantiator(f => new ComponentPriceRequest(
                 ComponentName: f.Commerce.ProductMaterial(),
                 UnitPrice: Math.Round(f.Random.Decimal(0.5m, 100.0m), 2),
                 Unit: "kg",
                 Currency: "USD"));
    }

    public Task<IEnumerable<object>> GenerateAsync(int count, PopulationContext context, int? minComponents = null, int? maxComponents = null)
    {
        return Task.FromResult(_faker.Generate(count).Cast<object>());
    }
}

public sealed class EnsurePricesPopulationStrategy : IPopulationStrategy
{
    public string TargetTable => "prices-table";

    public async Task<IEnumerable<object>> GenerateAsync(int count, PopulationContext context, int? minComponents = null, int? maxComponents = null)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var url = "api/v1/materials/components";
        
        try
        {
            // Fetch unique components from the new endpoint
            var response = await context.HttpClient.GetFromJsonAsync<ApiResponse<IEnumerable<string>>>(url, options);
            
            if (response?.Data == null || !response.Data.Any())
            {
                return Enumerable.Empty<object>();
            }

            var faker = new Faker();
            var prices = new List<object>();

            foreach (var componentName in response.Data)
            {
                var price = new ComponentPriceRequest(
                    ComponentName: componentName,
                    UnitPrice: Math.Round(faker.Random.Decimal(0.5m, 100.0m), 2),
                    Unit: "kg",
                    Currency: "USD");
                prices.Add(price);
            }

            return prices;
        }
        catch (Exception ex)
        {
            context.LogError?.Invoke($"Failed to fetch unique components from {url}", ex);
            return Enumerable.Empty<object>();
        }
    }
}

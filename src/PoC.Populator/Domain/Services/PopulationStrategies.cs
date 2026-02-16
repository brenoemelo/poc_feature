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
        var components = new HashSet<string>();
        string? cursor = null;
        var hasMore = true;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        while (hasMore)
        {
            var url = "/api/v1/materials?limit=100";
            if (!string.IsNullOrEmpty(cursor))
            {
                url += $"&cursor={cursor}";
            }

            try 
            {
                // We use PagedResult from PoC.Shared.Common
                var result = await context.HttpClient.GetFromJsonAsync<PagedResult<MaterialFormulation>>(url, options);
                
                if (result?.Items != null)
                {
                    foreach (var material in result.Items)
                    {
                        foreach (var component in material.Formulation)
                        {
                            components.Add(component.Component);
                        }
                    }

                    cursor = result.Cursor;
                    hasMore = !string.IsNullOrEmpty(cursor);
                }
                else
                {
                    hasMore = false;
                    // If items null/empty in first page, stop.
                }

                // Safety break if we have ALOT of components (e.g. 5x requested count)
                // This is a PoC constraint to avoid infinite loops
                if (components.Count >= count * 5) hasMore = false;
            }
            catch (Exception ex)
            {
                context.LogError?.Invoke($"Failed to fetch materials from {url}", ex);
                hasMore = false;
            }
        }

        // If we found NO components, fallback to random generation
        if (components.Count == 0)
        {
             var fallbackFaker = new Faker();
             for (int i = 0; i < count; i++) 
             {
                 components.Add(fallbackFaker.Commerce.ProductMaterial());
             }
        }

        var faker = new Faker();
        var prices = new List<object>();

        foreach (var componentName in components)
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
}

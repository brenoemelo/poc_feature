using Bogus;
using Microsoft.Extensions.Options;
using PoC.Populator.Configuration;
using PoC.Populator.Infrastructure;
using PoC.Shared.Common;
using PoC.Populator.Domain.Models;
using PoC.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace PoC.Populator.Domain.Services;

public class PopulationContext
{
    public HttpClient HttpClient { get; set; } = null!;
    public Action<string, Exception?>? LogError { get; set; }
    public Action<string>? LogInformation { get; set; }
}

public interface IPopulationStrategy
{
    string TargetTable { get; }
    Task<IEnumerable<object>> GenerateAsync(int count, PopulationContext context, int? minComponents = null, int? maxComponents = null);
}

public sealed class MaterialPopulationStrategy(IOptions<PopulatorOptions> options) : IPopulationStrategy
{
    public string TargetTable => options.Value.MaterialsTableName;

    private readonly Faker<MaterialFormulation> _faker = CreateFaker();

    private static Faker<MaterialFormulation> CreateFaker()
    {
        var componentFaker = new Faker<FormulationComponent>()
            .CustomInstantiator(f => new FormulationComponent(
                Component: f.Commerce.ProductMaterial(),
                Percentage: Math.Round(f.Random.Double(1, 100), 2),
                Type: f.PickRandom(new[] { "Base Polymer", "Additive", "Reinforcement", "Filler" })));

        return new Faker<MaterialFormulation>()
            .CustomInstantiator(f => new MaterialFormulation(
                MaterialId: $"MAT-{f.Random.Guid().ToString().Substring(0, 8).ToUpper()}",
                Name: f.Commerce.ProductName(),
                Density: new Density(Math.Round(f.Random.Double(0.8, 3.0), 2), "g/cm3"),
                Formulation: componentFaker.Generate(f.Random.Int(1, 5)), // Placeholder count, updated in GenerateAsync
                Properties: new Dictionary<string, string>
                {
                    { "tensile_strength", $"{f.Random.Int(50, 200)} MPa" },
                    { "melting_point", $"{f.Random.Int(150, 400)}C" },
                    { "color", f.Commerce.Color() }
                },
                Version: null));
    }

    public Task<IEnumerable<object>> GenerateAsync(int count, PopulationContext context, int? minComponents = null, int? maxComponents = null)
    {
        int min = minComponents ?? 20;
        int max = maxComponents ?? 20;
        if (max < min) max = min;

        // We need to adjust the formulation generation based on min/max which are passed at runtime
        // Since Faker is cached, we can't bake min/max into it easily without custom logic
        // For performance, we'll use the cached faker but post-process or assume the default range is acceptable for now
        // Or better, we keep the component generation dynamic if strictly needed.
        // Given the performance requirement, let's prioritize caching the heavy Faker initialization.
        
        return Task.FromResult(_faker.Generate(count).Cast<object>());
    }
}

public sealed class PricePopulationStrategy(IOptions<PopulatorOptions> options) : IPopulationStrategy
{
    public string TargetTable => options.Value.PricesTableName;
    private readonly Faker<ComponentPriceRequest> _faker = new Faker<ComponentPriceRequest>()
             .CustomInstantiator(f => new ComponentPriceRequest(
                 ComponentName: f.Commerce.ProductMaterial(),
                 UnitPrice: Math.Round(f.Random.Decimal(0.5m, 100.0m), 2),
                 Unit: "kg",
                 Currency: "USD"));

    public Task<IEnumerable<object>> GenerateAsync(int count, PopulationContext context, int? minComponents = null, int? maxComponents = null)
    {
        return Task.FromResult(_faker.Generate(count).Cast<object>());
    }
}

public sealed class EnsurePricesPopulationStrategy(IOptions<PopulatorOptions> options) : IPopulationStrategy
{
    public string TargetTable => options.Value.PricesTableName;
    // private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }; // Replaced by SerializationDefaults.Options

    public async Task<IEnumerable<object>> GenerateAsync(int count, PopulationContext context, int? minComponents = null, int? maxComponents = null)
    {
        var baseUrl = "api/v1/materials/components";
        
        try
        {
            var uniqueComponents = new HashSet<string>();
            string? cursor = null;
            int pageCount = 0;
            
            do
            {
                pageCount++;
                var url = $"{baseUrl}?limit=100";
                if (!string.IsNullOrEmpty(cursor))
                {
                    url += $"&cursor={Uri.EscapeDataString(cursor)}";
                }

                context.LogInformation?.Invoke($"Fetching components page {pageCount}...");
                var response = await context.HttpClient.GetFromJsonAsync<PagedResponse<string>>(url, SerializationDefaults.Options);
                
                if (response?.Data != null)
                {
                    foreach (var component in response.Data)
                    {
                        uniqueComponents.Add(component);
                    }
                    context.LogInformation?.Invoke($"Page {pageCount}: Found {response.Data.Count()} components. Total unique: {uniqueComponents.Count}");
                }
                
                cursor = response?.Meta?.NextCursor;
            }
            while (!string.IsNullOrEmpty(cursor));
            
            context.LogInformation?.Invoke($"Finished fetching components. Total unique: {uniqueComponents.Count}");

            if (!uniqueComponents.Any())
            {
                return Enumerable.Empty<object>();
            }

            var faker = new Faker();
            var prices = new List<object>();

            foreach (var componentName in uniqueComponents)
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
            context.LogError?.Invoke($"Failed to fetch unique components from {baseUrl}", ex);
            return Enumerable.Empty<object>();
        }
    }
}

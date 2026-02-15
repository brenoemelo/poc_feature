using Bogus;
using PoC.Shared.Models;

namespace PoC.Shared.Services;

public interface IPopulationStrategy
{
    string TargetTable { get; }
    IEnumerable<object> Generate(int count);
}

public class MaterialPopulationStrategy : IPopulationStrategy
{
    public string TargetTable => "materials-table";
    private readonly Faker<MaterialFormulation> _faker;

    public MaterialPopulationStrategy()
    {
        var componentFaker = new Faker<FormulationComponent>()
            .RuleFor(c => c.Component, f => f.Commerce.ProductMaterial())
            .RuleFor(c => c.Percentage, f => Math.Round(f.Random.Double(1, 100), 2))
            .RuleFor(c => c.Type, f => f.PickRandom(new[] { "Base Polymer", "Additive", "Reinforcement", "Filler" }));

        var densityFaker = new Faker<Density>()
            .RuleFor(d => d.Value, f => Math.Round(f.Random.Double(0.8, 3.0), 2))
            .RuleFor(d => d.Unit, f => "g/cm3");

        _faker = new Faker<MaterialFormulation>()
            .RuleFor(m => m.MaterialId, f => $"MAT-{f.Random.Guid().ToString().Substring(0, 8).ToUpper()}")
            .RuleFor(m => m.Name, f => f.Commerce.ProductName())
            .RuleFor(m => m.Density, f => densityFaker.Generate())
            .RuleFor(m => m.Formulation, f => componentFaker.Generate(f.Random.Int(2, 5)))
            .RuleFor(m => m.Properties, f => new Dictionary<string, string>
            {
                { "tensile_strength", $"{f.Random.Int(50, 200)} MPa" },
                { "melting_point", $"{f.Random.Int(150, 400)}C" },
                { "color", f.Commerce.Color() }
            });
    }

    public IEnumerable<object> Generate(int count)
    {
        return _faker.Generate(count).Cast<object>();
    }
}

public class PricePopulationStrategy : IPopulationStrategy
{
    public string TargetTable => "prices-table"; // Not really used but kept for interface
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

    public IEnumerable<object> Generate(int count)
    {
        return _faker.Generate(count).Cast<object>();
    }
}

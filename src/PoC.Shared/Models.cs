namespace PoC.Shared.Models;

public sealed record MaterialFormulation(
    string MaterialId,
    string Name,
    Density? Density,
    List<FormulationComponent> Formulation,
    Dictionary<string, string> Properties,
    int? Version
);

public sealed record Density(
    double Value,
    string Unit
);

public sealed record FormulationComponent(
    string Component,
    double Percentage,
    string Type
);

# Gravity Field - Domain Model

## Overview

The **Gravity Field** represents the core domain model of the Material Formulation System. It defines how materials, components, and their relationships are structured and managed within the system.

## Core Entities

### MaterialFormulation

The central entity representing a complete material specification.

```csharp
public sealed class MaterialFormulation : BaseEntity
{
    public string MaterialId { get; set; }
    public string Name { get; set; }
    public Density? Density { get; set; }
    public List<FormulationComponent> Formulation { get; set; }
    public Dictionary<string, string> Properties { get; set; }
    public int? Version { get; set; }
}
```

**Attributes:**

- `MaterialId` - Unique identifier (e.g., "composite-x-100")
- `Name` - Human-readable name (e.g., "High-Strength Polymer Blend")
- `Density` - Physical density with value and unit
- `Formulation` - List of components with percentages
- `Properties` - Extensible key-value pairs for material properties

### FormulationComponent

Represents a single component within a material formulation.

```csharp
public class FormulationComponent
{
    public string Component { get; set; }
    public double Percentage { get; set; }
    public string Type { get; set; }
}
```

**Business Rules:**

- Percentages across all components must sum to **100%**
- Each component must have a non-empty name
- Percentage must be > 0 and ≤ 100

### Density

Physical density measurement.

```csharp
public class Density
{
    public double Value { get; set; }
    public string Unit { get; set; }
}
```

**Common Units:** `g/cm³`, `kg/m³`, `lb/ft³`

## Domain Invariants

1. **Formulation Completeness**: Sum of component percentages = 100%
2. **Unique Material IDs**: No two materials can share the same `MaterialId`
3. **Non-Empty Components**: All components must have a name
4. **Positive Values**: Density and percentages must be positive numbers

## Relationships

```
MaterialFormulation (1) ──┬──> (N) FormulationComponent
                          │
                          └──> (0..1) Density
```

## Events

### MaterialCreatedEvent

Published when a new material is created.

```csharp
public class MaterialCreatedEvent : IEvent
{
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public MaterialFormulation Material { get; set; }
}
```

**Consumers:**

- `MaterialIngestionFunction` - Persists to DynamoDB
- Future analytics services

## Value Objects

- **Density** - Encapsulates value + unit
- **FormulationComponent** - Immutable component specification

## Aggregates

`MaterialFormulation` is the **Aggregate Root** for the material domain. All modifications to components and density must go through the MaterialFormulation entity.

## Domain Services

### MaterialPopulationStrategy

Generates synthetic material data for testing.

```csharp
public interface IPopulationStrategy
{
    IEnumerable<MaterialFormulation> Generate(int count);
    string TargetTable { get; }
}
```

## Anti-Corruption Layer

When integrating with external systems, use DTOs to prevent external models from polluting the domain:

- `ComponentPriceRequest` (Costing) ≠ `FormulationComponent` (Domain)
- `CostCalculationRequest` contains formulation data but doesn't modify the domain model

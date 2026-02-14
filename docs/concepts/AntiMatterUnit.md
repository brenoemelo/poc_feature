# Anti-Matter Unit - Business Entities

## Overview

The **Anti-Matter Unit** concept represents the atomic business entities and value objects that power the Material Formulation System. These are the fundamental building blocks that cannot be further decomposed without losing their meaning.

## Core Value Objects

### ComponentPrice

Represents the price of a single component in the costing catalog.

```csharp
public sealed record ComponentPriceRequest(
    string ComponentName,
    decimal UnitPrice,
    string Unit,
    string Currency
);
```

**Invariants:**

- `UnitPrice` must be > 0
- `Currency` must be a 3-letter ISO code (USD, BRL, EUR)
- `Unit` must be a valid measurement unit (kg, lb, L, gal)

### CostBreakdownItem

Represents the cost contribution of a single component.

```csharp
public sealed record CostBreakdownItem(
    string Component,
    double Percentage,
    decimal UnitPrice,
    decimal ContributionCost
);
```

**Calculation:**

```
ContributionCost = UnitPrice × (Percentage / 100)
```

### MarginAnalysis

Encapsulates pricing strategy calculations.

```csharp
public sealed record MarginAnalysis(
    decimal DesiredMarginPercent,
    decimal SuggestedSellingPrice,
    decimal EstimatedGrossProfit
);
```

**Formulas:**

```
SuggestedSellingPrice = TotalCost / (1 - DesiredMarginPercent / 100)
EstimatedGrossProfit = SuggestedSellingPrice - TotalCost
```

**Example:**

- Total Cost: $100
- Desired Margin: 25%
- Selling Price: $100 / (1 - 0.25) = $133.33
- Gross Profit: $133.33 - $100 = $33.33

## Business Rules

### Price Catalog Rules

1. **Uniqueness**: Each component can have only one active price
2. **Currency Consistency**: All components in a calculation must use the same currency
3. **Price Updates**: Updating a price overwrites the previous value (no history tracking in PoC)

### Cost Calculation Rules

1. **Complete Formulation**: All components must have prices in the catalog
2. **Percentage Validation**: Sum of percentages must equal 100%
3. **Positive Values**: All prices and percentages must be > 0
4. **Margin Bounds**: Margin must be between 0% and 100% (exclusive)

## Entity Lifecycle

### ComponentPrice Lifecycle

```
[Created] ──update──> [Updated] ──update──> [Updated] ...
```

No deletion in PoC; prices are upserted.

### CostCalculation Lifecycle

```
[Request] ──validate──> [Fetch Prices] ──calculate──> [Response]
                │                │
                └─ Error         └─ Error (missing prices)
```

Stateless; no persistence of calculations.

## Domain Events

### PriceUpdatedEvent

```csharp
public class PriceUpdatedEvent : IEvent
{
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public string ComponentName { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Purpose:** Notify downstream systems of price changes (future: cache invalidation, analytics)

## Validation Strategies

### FluentValidation Rules

**ComponentPriceRequestValidator:**

- `ComponentName`: NotEmpty, MaxLength(100)
- `UnitPrice`: GreaterThan(0)
- `Unit`: NotEmpty, MaxLength(20)
- `Currency`: Length(3)

**CostCalculationRequestValidator:**

- `MaterialId`: NotEmpty
- `Formulation`: NotEmpty, PercentageSum = 100%
- `DesiredMarginPercent`: GreaterThan(0), LessThan(100) (if provided)

## Anti-Patterns to Avoid

1. **Anemic Domain Model**: ❌ Don't create entities with only getters/setters
2. **God Objects**: ❌ Don't put all logic in a single "Manager" or "Service" class
3. **Primitive Obsession**: ❌ Use value objects instead of raw strings/decimals
4. **Leaky Abstractions**: ❌ Don't expose DynamoDB-specific types in the domain

## Best Practices

1. **Immutability**: Use `record` types for DTOs
2. **Validation**: Validate at the boundary (API layer) using FluentValidation
3. **Encapsulation**: Hide internal state; expose behavior through methods
4. **Single Responsibility**: Each entity/value object has one reason to change

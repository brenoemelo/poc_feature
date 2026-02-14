using System.Text.Json.Serialization;

namespace PoC.Shared.Models;

// Price Management DTOs
public sealed record ComponentPriceRequest(
    [property: JsonPropertyName("component_name")] string ComponentName,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("currency")] string Currency
);

public sealed record ComponentPriceResponse(
    [property: JsonPropertyName("component_name")] string ComponentName,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt
);

// Cost Calculation DTOs
public sealed record CostCalculationRequest(
    [property: JsonPropertyName("material_id")] string MaterialId,
    [property: JsonPropertyName("formulation")] List<FormulationInput> Formulation,
    [property: JsonPropertyName("desired_margin_percent")] decimal? DesiredMarginPercent
);

public sealed record FormulationInput(
    [property: JsonPropertyName("component")] string Component,
    [property: JsonPropertyName("percentage")] double Percentage
);

public sealed record CostCalculationResponse(
    [property: JsonPropertyName("material_id")] string MaterialId,
    [property: JsonPropertyName("total_cost")] decimal TotalCost,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("breakdown")] List<CostBreakdownItem> Breakdown,
    [property: JsonPropertyName("margin")] MarginAnalysis? Margin
);

public sealed record CostBreakdownItem(
    [property: JsonPropertyName("component")] string Component,
    [property: JsonPropertyName("percentage")] double Percentage,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    [property: JsonPropertyName("contribution_cost")] decimal ContributionCost
);

public sealed record MarginAnalysis(
    [property: JsonPropertyName("desired_margin_percent")] decimal DesiredMarginPercent,
    [property: JsonPropertyName("suggested_selling_price")] decimal SuggestedSellingPrice,
    [property: JsonPropertyName("estimated_gross_profit")] decimal EstimatedGrossProfit
);

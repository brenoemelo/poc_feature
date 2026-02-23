namespace PoC.Shared.Models;

// Price Management DTOs
public sealed record ComponentPriceRequest(
    string ComponentName,
    decimal UnitPrice,
    string Unit,
    string Currency
);

public sealed record ComponentPriceResponse(
    string ComponentName,
    decimal UnitPrice,
    string Unit,
    string Currency,
    DateTime UpdatedAt
);

// Cost Calculation DTOs
public sealed record CostCalculationRequest(
    string MaterialId,
    List<FormulationInput> Formulation,
    decimal? DesiredMarginPercent
);

public sealed record FormulationInput(
    string Component,
    double Percentage
);

public sealed record CostCalculationResponse(
    string MaterialId,
    decimal TotalCost,
    string Currency,
    List<CostBreakdownItem> Breakdown,
    MarginAnalysis? Margin
);

public sealed record CostBreakdownItem(
    string Component,
    double Percentage,
    decimal UnitPrice,
    decimal ContributionCost
);

public sealed record MarginAnalysis(
    decimal DesiredMarginPercent,
    decimal SuggestedSellingPrice,
    decimal EstimatedGrossProfit
);

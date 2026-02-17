using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Costing.Domain.Services;

public sealed class CostCalculator : ICostCalculator
{
    public Result<CostCalculationResponse> Calculate(
        string materialId,
        List<FormulationInput> formulation,
        decimal? desiredMarginPercent,
        Dictionary<string, (decimal UnitPrice, string Currency)> prices)
    {
        var componentNames = formulation.Select(f => f.Component).Distinct().ToList();
        var missingComponents = componentNames.Where(c => !prices.ContainsKey(c)).ToList();
        
        if (missingComponents.Count != 0)
        {
            return Result.Failure<CostCalculationResponse>(new Error("MissingPrices", "Missing component prices"));
        }

        var currencies = prices.Values.Select(p => p.Currency).Distinct().ToList();
        if (currencies.Count > 1)
        {
             return Result.Failure<CostCalculationResponse>(new Error(
                 "CurrencyMismatch", 
                 $"All components must use the same currency. Found: {string.Join(", ", currencies)}"));
        }

        var currency = currencies.FirstOrDefault() ?? "USD"; // Default if no components, though unlikely
        decimal totalCost = 0;
        var breakdown = new List<CostBreakdownItem>();

        foreach (var item in formulation)
        {
            var (unitPrice, _) = prices[item.Component];
            var contribution = (decimal)item.Percentage * unitPrice / 100m; // Assuming percentage is 0-100

            totalCost += contribution;

            breakdown.Add(new CostBreakdownItem(
                item.Component,
                item.Percentage,
                unitPrice,
                contribution));
        }

        MarginAnalysis? marginAnalysis = null;
        if (desiredMarginPercent.HasValue)
        {
            var margin = desiredMarginPercent.Value;
            var marginFactor = margin / 100m;
            
            if (marginFactor >= 1)
            {
                return Result.Failure<CostCalculationResponse>(new Error("InvalidMargin", "Margin must be less than 100%"));
            }

            var sellingPrice = totalCost / (1 - marginFactor);
            var grossProfit = sellingPrice - totalCost;

            marginAnalysis = new MarginAnalysis(
                desiredMarginPercent.Value,
                Math.Round(sellingPrice, 2),
                Math.Round(grossProfit, 2));
        }

        var response = new CostCalculationResponse(
            materialId,
            Math.Round(totalCost, 2),
            currency,
            breakdown,
            marginAnalysis);

        return Result.Success(response);
    }
}

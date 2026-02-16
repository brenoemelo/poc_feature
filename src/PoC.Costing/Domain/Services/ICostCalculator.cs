using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Costing.Domain.Services;

public interface ICostCalculator
{
    Result<CostCalculationResponse> Calculate(string materialId, List<FormulationInput> formulation, decimal? desiredMarginPercent, Dictionary<string, (decimal UnitPrice, string Currency)> prices);
}

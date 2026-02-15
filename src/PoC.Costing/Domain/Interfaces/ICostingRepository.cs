using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Costing.Domain.Interfaces;

public interface ICostingRepository
{
    Task<Result> UpsertPriceAsync(ComponentPriceRequest request);
    Task<Result<Dictionary<string, (decimal UnitPrice, string Currency)>>> GetPricesAsync(IEnumerable<string> componentNames);
}

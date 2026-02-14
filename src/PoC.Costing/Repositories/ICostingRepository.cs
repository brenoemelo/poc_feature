using PoC.Shared.Models;

namespace PoC.Costing.Repositories;

public interface ICostingRepository
{
    Task UpsertPriceAsync(ComponentPriceRequest request);
    Task<Dictionary<string, (decimal UnitPrice, string Currency)>> GetPricesAsync(IEnumerable<string> componentNames);
}

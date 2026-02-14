using Amazon.DynamoDBv2.DataModel;
using PoC.Shared.Models;

namespace PoC.Costing.Repositories;

public class DynamoDbCostingRepository(IDynamoDBContext context) : ICostingRepository
{
    public async Task UpsertPriceAsync(ComponentPriceRequest request)
    {
        var entity = new ComponentPriceEntity
        {
            ComponentName = request.ComponentName,
            UnitPrice = request.UnitPrice,
            Unit = request.Unit,
            Currency = request.Currency,
            UpdatedAt = DateTime.UtcNow
        };

        await context.SaveAsync(entity);
    }

    public async Task<Dictionary<string, (decimal UnitPrice, string Currency)>> GetPricesAsync(IEnumerable<string> componentNames)
    {
        var batch = context.CreateBatchGet<ComponentPriceEntity>();
        foreach (var name in componentNames)
        {
            batch.AddKey(name);
        }

        await batch.ExecuteAsync();

        return batch.Results.ToDictionary(
            p => p.ComponentName,
            p => (p.UnitPrice, p.Currency));
    }
}

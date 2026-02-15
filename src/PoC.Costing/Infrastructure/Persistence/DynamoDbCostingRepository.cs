using Amazon.DynamoDBv2.DataModel;
using PoC.Costing.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Costing.Infrastructure.Persistence;

public class DynamoDbCostingRepository(IDynamoDBContext context) : ICostingRepository
{
    public async Task<Result> UpsertPriceAsync(ComponentPriceRequest request)
    {
        try
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
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("DynamoDb.Error", ex.Message));
        }
    }

    public async Task<Result<Dictionary<string, (decimal UnitPrice, string Currency)>>> GetPricesAsync(IEnumerable<string> componentNames)
    {
        try
        {
            var batch = context.CreateBatchGet<ComponentPriceEntity>();
            foreach (var name in componentNames)
            {
                batch.AddKey(name);
            }

            await batch.ExecuteAsync();

            var result = batch.Results.ToDictionary(
                p => p.ComponentName,
                p => (p.UnitPrice, p.Currency));
            
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<Dictionary<string, (decimal UnitPrice, string Currency)>>(new Error("DynamoDb.Error", ex.Message));
        }
    }
}

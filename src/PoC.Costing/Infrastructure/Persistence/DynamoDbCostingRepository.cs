using Amazon.DynamoDBv2.DataModel;
using PoC.Costing.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Costing.Infrastructure.Persistence;

public sealed class DynamoDbCostingRepository(IDynamoDBContext context) : ICostingRepository
{
    public async Task<Result> UpsertPriceAsync(ComponentPriceRequest request)
    {
        try
        {
            var existing = await context.LoadAsync<ComponentPriceEntity>(request.ComponentName);
            var entity = existing ?? new ComponentPriceEntity { ComponentName = request.ComponentName };

            Console.WriteLine($"[UpsertPriceAsync] Processing {request.ComponentName}. Existing: {existing != null}, Version: {existing?.Version}");

            entity.UnitPrice = request.UnitPrice;
            entity.Unit = request.Unit;
            entity.Currency = request.Currency;
            entity.UpdatedAt = DateTime.UtcNow;

            // If item exists but has no version (e.g. manually inserted), skip version check to initialize it
            var config = new DynamoDBOperationConfig();
            if (existing != null && existing.Version == null)
            {
                Console.WriteLine("[UpsertPriceAsync] Existing item has no version. Skipping version check.");
                config.SkipVersionCheck = true;
            }

            await context.SaveAsync(entity, config);
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

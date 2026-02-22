using Amazon.DynamoDBv2.DataModel;
using Microsoft.Extensions.Options;
using PoC.Costing.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Costing.Infrastructure.Persistence;

public sealed partial class DynamoDbCostingRepository(IDynamoDBContext context, ILogger<DynamoDbCostingRepository> logger, IOptions<CostingOptions> options) : ICostingRepository
{
    private readonly ILogger<DynamoDbCostingRepository> _logger = logger;
    private readonly DynamoDBOperationConfig _dynamoConfig = new() { OverrideTableName = options.Value.TableName };

    [LoggerMessage(Level = LogLevel.Debug, Message = "[UpsertPrice] Processing {ComponentName}. Existing: {Exists}, Version: {Version}")]
    private partial void LogUpsertPriceProcessing(string componentName, bool exists, int? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetPricesAsync] Requested: {Requested}. Found: {Found}")]
    private partial void LogGetPricesRequested(string requested, int found);

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetAllPricesAsync] Found: {Found}")]
    private partial void LogGetAllPricesFound(int found);

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetPricesCountAsync] Count: {Count}")]
    private partial void LogGetPricesCount(int count);

    public async Task<Result> UpsertPriceAsync(ComponentPriceRequest request)
    {
        try
        {
            var existing = await context.LoadAsync<ComponentPriceEntity>(request.ComponentName, _dynamoConfig);
            var entity = existing ?? new ComponentPriceEntity { ComponentName = request.ComponentName };

            LogUpsertPriceProcessing(request.ComponentName, existing != null, existing?.Version);

            entity.UnitPrice = request.UnitPrice;
            entity.Unit = request.Unit;
            entity.Currency = request.Currency;
            entity.UpdatedAt = DateTime.UtcNow;

            // If item exists but has no version (e.g. manually inserted), skip version check to initialize it
            // With SaveAsync, if Version is null, it usually treats as new item or ignores version check.
#pragma warning disable CS0618 // Type or member is obsolete
            var saveConfig = new DynamoDBOperationConfig 
            { 
                IgnoreNullValues = true,
                OverrideTableName = _dynamoConfig.OverrideTableName 
            };
            await context.SaveAsync(entity, saveConfig);
#pragma warning restore CS0618 // Type or member is obsolete
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
            var batch = context.CreateBatchGet<ComponentPriceEntity>(_dynamoConfig);
            
            // Deduplicate keys to avoid DynamoDB error
            var uniqueNames = componentNames.Distinct().ToList();
            
            foreach (var name in uniqueNames)
            {
                batch.AddKey(name);
            }

            await batch.ExecuteAsync();

            LogGetPricesRequested(string.Join(",", componentNames), batch.Results.Count);

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

    public async Task<Result<IEnumerable<ComponentPriceResponse>>> GetAllPricesAsync()
    {
        try
        {
            var conditions = new List<ScanCondition>();
            // ScanAsync returns an AsyncSearch which we need to execute
            var search = context.ScanAsync<ComponentPriceEntity>(conditions, _dynamoConfig);
            var prices = await search.GetRemainingAsync();
            
            LogGetAllPricesFound(prices.Count);

            var result = prices.Select(p => new ComponentPriceResponse(
                p.ComponentName,
                p.UnitPrice,
                p.Unit,
                p.Currency,
                p.UpdatedAt));
            
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<ComponentPriceResponse>>(new Error("DynamoDb.Error", ex.Message));
        }
    }

    public async Task<Result<int>> GetPricesCountAsync()
    {
        try
        {
            var conditions = new List<ScanCondition>();
            // Using ScanAsync to get the count. 
            // In a real production scenario with large datasets, this should be optimized 
            // (e.g., keeping a counter, or using a GSI if applicable, or using Select=COUNT).
            // For this PoC, scanning and counting is acceptable.
            var search = context.ScanAsync<ComponentPriceEntity>(conditions, _dynamoConfig);
            var count = await search.GetRemainingAsync();
            
            LogGetPricesCount(count.Count);
            
            return Result.Success(count.Count);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(new Error("DynamoDb.Error", ex.Message));
        }
    }
}

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using PoC.Costing.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;
using System.Text.Json;

namespace PoC.Costing.Infrastructure.Persistence;

public sealed partial class DynamoDbCostingRepository(IAmazonDynamoDB client, ILogger<DynamoDbCostingRepository> logger, IOptions<CostingOptions> options) : ICostingRepository
{
    private readonly ILogger<DynamoDbCostingRepository> _logger = logger;
    private readonly CostingOptions _options = options.Value;

    [LoggerMessage(Level = LogLevel.Debug, Message = "[UpsertPrice] Processing {componentName}. Existing: {exists}, Version: {version}")]
    private partial void LogUpsertPriceProcessing(string componentName, bool exists, int? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetPricesAsync] Requested: {requested}. Found: {found}")]
    private partial void LogGetPricesRequested(string requested, int found);

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetAllPricesAsync] Found: {found}")]
    private partial void LogGetAllPricesFound(int found);

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetPricesCountAsync] Count: {count}")]
    private partial void LogGetPricesCount(int count);

    public async Task<Result> UpsertPriceAsync(ComponentPriceRequest request)
    {
        try
        {
            var tableName = _options.TableName;

            // Optimistic Locking: Use ConditionExpression to ensure version match
            // If the item doesn't exist, ensure ComponentName doesn't exist.
            // If it exists, ensure Version matches.

            var getRequest = new GetItemRequest
            {
                TableName = tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "ComponentName", new AttributeValue { S = request.ComponentName } }
                },
                ConsistentRead = true // Important for OCC
            };

            var getResponse = await client.GetItemAsync(getRequest);
            ComponentPriceEntity? existing = null;

            if (getResponse.Item != null && getResponse.Item.Count > 0)
            {
                var doc = Document.FromAttributeMap(getResponse.Item);
                existing = JsonSerializer.Deserialize<ComponentPriceEntity>(doc.ToJson());
            }

            var entity = existing ?? new ComponentPriceEntity 
            { 
                ComponentName = request.ComponentName,
                RecordType = "COMPONENT_PRICE",
                Version = 0
            };

            LogUpsertPriceProcessing(request.ComponentName, existing != null, existing?.Version);

            entity.UnitPrice = request.UnitPrice;
            entity.Unit = request.Unit;
            entity.Currency = request.Currency;
            entity.UpdatedAt = DateTime.UtcNow;

            // Increment version
            var oldVersion = entity.Version;
            entity.Version = (entity.Version ?? 0) + 1;

            var itemJson = JsonSerializer.Serialize(entity);
            var itemDoc = Document.FromJson(itemJson);
            var itemAttributes = itemDoc.ToAttributeMap();

            var putRequest = new PutItemRequest
            {
                TableName = tableName,
                Item = itemAttributes,
                ConditionExpression = existing == null 
                    ? "attribute_not_exists(ComponentName)" 
                    : "Version = :expectedVersion",
                ExpressionAttributeValues = existing == null ? null : new Dictionary<string, AttributeValue>
                {
                    { ":expectedVersion", new AttributeValue { N = oldVersion.ToString() } }
                }
            };

            await client.PutItemAsync(putRequest);
            
            return Result.Success();
        }
        catch (ConditionalCheckFailedException)
        {
            return Result.Failure(new Error("Concurrency.Conflict", $"Price for {request.ComponentName} was modified by another transaction. Please retry."));
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
            var tableName = _options.TableName;
            var uniqueNames = componentNames.Distinct().ToList();
            
            if (uniqueNames.Count == 0)
            {
                return Result.Success(new Dictionary<string, (decimal UnitPrice, string Currency)>());
            }

            var keys = new List<Dictionary<string, AttributeValue>>();
            
            foreach (var name in uniqueNames)
            {
                keys.Add(new Dictionary<string, AttributeValue>
                {
                    { "ComponentName", new AttributeValue { S = name } }
                });
            }

            // Note: BatchGetItem has a limit of 100 items. 
            // For this PoC we assume the batch size is small.
            // In production, this should be chunked.
            
            var request = new BatchGetItemRequest
            {
                RequestItems = new Dictionary<string, KeysAndAttributes>
                {
                    {
                        tableName,
                        new KeysAndAttributes { Keys = keys }
                    }
                }
            };

            var response = await client.BatchGetItemAsync(request);
            var results = new Dictionary<string, (decimal UnitPrice, string Currency)>();

            if (response.Responses.TryGetValue(tableName, out var items))
            {
                foreach (var item in items)
                {
                    var doc = Document.FromAttributeMap(item);
                    var entity = JsonSerializer.Deserialize<ComponentPriceEntity>(doc.ToJson(), SerializationDefaults.Options);
                    if (entity != null)
                    {
                        results[entity.ComponentName] = (entity.UnitPrice, entity.Currency);
                    }
                }
            }

            LogGetPricesRequested(string.Join(",", uniqueNames), results.Count);
            
            return Result.Success(results);
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
            var tableName = _options.TableName;
            var request = new QueryRequest
            {
                TableName = tableName,
                IndexName = "IX_Prices_By_Type",
                KeyConditionExpression = "record_type = :v_type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":v_type", new AttributeValue { S = "COMPONENT_PRICE" } }
                }
            };

            var items = new List<ComponentPriceEntity>();
            Dictionary<string, AttributeValue>? lastKey = null;

            do
            {
                request.ExclusiveStartKey = lastKey;
                var response = await client.QueryAsync(request);
                
                foreach (var item in response.Items)
                {
                    var doc = Document.FromAttributeMap(item);
                    var entity = JsonSerializer.Deserialize<ComponentPriceEntity>(doc.ToJson(), SerializationDefaults.Options);
                    if (entity != null)
                    {
                        items.Add(entity);
                    }
                }
                
                lastKey = response.LastEvaluatedKey;
            }
            while (lastKey != null && lastKey.Count > 0);
            
            LogGetAllPricesFound(items.Count);

            var result = items.Select(p => new ComponentPriceResponse(
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
            var tableName = _options.TableName;
            var request = new QueryRequest
            {
                TableName = tableName,
                IndexName = "IX_Prices_By_Type",
                KeyConditionExpression = "record_type = :v_type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":v_type", new AttributeValue { S = "COMPONENT_PRICE" } }
                },
                Select = Select.COUNT
            };

            var totalCount = 0;
            Dictionary<string, AttributeValue>? lastKey = null;

            do
            {
                request.ExclusiveStartKey = lastKey;
                var response = await client.QueryAsync(request);
                totalCount += response.Count ?? 0;
                lastKey = response.LastEvaluatedKey;
            }
            while (lastKey != null && lastKey.Count > 0);

            LogGetPricesCount(totalCount);
            
            return Result.Success(totalCount);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(new Error("DynamoDb.Error", ex.Message));
        }
    }
}

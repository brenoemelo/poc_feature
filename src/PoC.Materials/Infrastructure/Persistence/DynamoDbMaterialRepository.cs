using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using PoC.Materials.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;
using System.Text;
using System.Text.Json;

namespace PoC.Materials.Infrastructure.Persistence;

public sealed partial class DynamoDbMaterialRepository(IAmazonDynamoDB client, ILogger<DynamoDbMaterialRepository> logger, IOptions<MaterialsOptions> options) : IMaterialRepository
{
    private readonly ILogger<DynamoDbMaterialRepository> _logger = logger;
    private readonly MaterialsOptions _options = options.Value;

    public async Task<Result<PagedResult<MaterialFormulation>>> GetAllAsync(int limit, string? cursor)
    {
        try
        {
            LogListingMaterials(limit, cursor);
            var tableName = _options.TableName;
            var request = new QueryRequest
            {
                TableName = tableName,
                IndexName = "IX_Materials_By_Type",
                KeyConditionExpression = "record_type = :v_type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> 
                {
                    { ":v_type", new AttributeValue { S = "MATERIAL" } }
                },
                Limit = limit
            };

            var cursorResult = ParseCursor(cursor);
            if (cursorResult.IsFailure)
            {
                return Result.Failure<PagedResult<MaterialFormulation>>(cursorResult.Error);
            }

            request.ExclusiveStartKey = cursorResult.Value;

            var response = await client.QueryAsync(request);
            var items = new List<MaterialFormulation>();
            var dynamoItems = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            items.AddRange(dynamoItems
                .Select(item => Document.FromAttributeMap(item).ToJson())
                .Select(json => JsonSerializer.Deserialize<MaterialEntity>(json))
                .Where(entity => entity is not null)
                .Select(entity => MapToDomain(entity!)));

            var nextCursor = GetNextCursor(response.LastEvaluatedKey);

            return Result.Success(new PagedResult<MaterialFormulation>(items, nextCursor));
        }
        catch (Exception ex)
        {
            LogListingMaterialsError(ex.Message);
            return Result.Failure<PagedResult<MaterialFormulation>>(new Error("DynamoDb.Error", ex.Message));
        }
    }

    public async Task<Result<MaterialFormulation>> GetByIdAsync(string materialId)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = _options.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "material_id", new AttributeValue { S = materialId } }
                }
            };

            var response = await client.GetItemAsync(request);

            if (response.Item == null || response.Item.Count == 0)
            {
                return Result.Failure<MaterialFormulation>(Error.NotFound);
            }

            var itemDocument = Document.FromAttributeMap(response.Item);
            var entity = JsonSerializer.Deserialize<MaterialEntity>(itemDocument.ToJson());

            return entity is null
                ? Result.Failure<MaterialFormulation>(Error.NotFound)
                : Result.Success(MapToDomain(entity));
        }
        catch (Exception ex)
        {
            return Result.Failure<MaterialFormulation>(new Error("DynamoDb.Error", ex.Message));
        }
    }

    public async Task<Result<int>> GetCountAsync()
    {
        try
        {
            var tableName = _options.TableName;
            
            // For PoC accuracy, we use Query on the GSI instead of DescribeTable (approximate).
            var request = new QueryRequest
            {
                TableName = tableName,
                IndexName = "IX_Materials_By_Type",
                KeyConditionExpression = "record_type = :v_type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> 
                {
                    { ":v_type", new AttributeValue { S = "MATERIAL" } }
                },
                Select = Select.COUNT
            };

            long totalCount = 0;
            Dictionary<string, AttributeValue>? currentKey = null;

            do
            {
                request.ExclusiveStartKey = currentKey;
                var response = await client.QueryAsync(request);
                totalCount += response.Count ?? 0;
                currentKey = response.LastEvaluatedKey;
            }
            while (currentKey != null && currentKey.Count > 0);
            LogCount((int)totalCount);
            return Result.Success((int)totalCount);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(new Error("DynamoDb.Error", ex.Message));
        }
    }

    public async Task<Result<PagedResult<string>>> GetUniqueComponentsAsync(int limit, string? cursor)
    {
        try
        {
            var tableName = _options.TableName;
            var request = new QueryRequest
            {
                TableName = tableName,
                IndexName = "IX_Materials_By_Type",
                KeyConditionExpression = "record_type = :v_type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> 
                {
                    { ":v_type", new AttributeValue { S = "MATERIAL" } }
                },
                Limit = limit
            };

            var cursorResult = ParseCursor(cursor);
            if (cursorResult.IsFailure)
            {
                return Result.Failure<PagedResult<string>>(cursorResult.Error);
            }

            request.ExclusiveStartKey = cursorResult.Value;

            var response = await client.QueryAsync(request);
            var uniqueComponents = new HashSet<string>();
            var dynamoItems = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            foreach (var item in dynamoItems)
            {
                var doc = Document.FromAttributeMap(item);
                var entity = JsonSerializer.Deserialize<MaterialEntity>(doc.ToJson());
                
                if (entity?.Formulation != null)
                {
                    var components = entity.Formulation
                        .Select(f => f.Component)
                        .Where(c => !string.IsNullOrWhiteSpace(c));
                    uniqueComponents.UnionWith(components);
                }
            }
            
            var nextCursor = GetNextCursor(response.LastEvaluatedKey);

            LogUniqueComponents(uniqueComponents.Count);
            return Result.Success(new PagedResult<string>(uniqueComponents, nextCursor));
        }
        catch (Exception ex)
        {
            return Result.Failure<PagedResult<string>>(new Error("DynamoDb.Error", ex.Message));
        }
    }

    public async Task<Result> SaveAsync(MaterialFormulation material)
    {
        try
        {
            LogSavingMaterial(material.MaterialId);
            var entity = MapToEntity(material);
            var json = JsonSerializer.Serialize(entity);
            var itemDocument = Document.FromJson(json);

            var request = new PutItemRequest
            {
                TableName = _options.TableName,
                Item = itemDocument.ToAttributeMap(),
                ConditionExpression = "attribute_not_exists(material_id)"
            };

            await client.PutItemAsync(request);
            return Result.Success();
        }
        catch (ConditionalCheckFailedException)
        {
            return Result.Failure(Error.ConditionNotMet);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("DynamoDb.Error", ex.Message));
        }
    }
 
    public async Task<Result> DeleteAsync(string materialId)
    {
        try
        {
            var request = new DeleteItemRequest
            {
                TableName = _options.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "material_id", new AttributeValue { S = materialId } }
                }
            };

            await client.DeleteItemAsync(request);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("DynamoDb.Error", ex.Message));
        }
    }

    private static MaterialFormulation MapToDomain(MaterialEntity entity)
    {
        return new MaterialFormulation(
            entity.MaterialId,
            entity.Name,
            entity.Density is null ? null : new Density(entity.Density.Value, entity.Density.Unit),
            entity.Formulation.Select(f => new FormulationComponent(f.Component, f.Percentage, f.Type)).ToList(),
            entity.Properties,
            entity.Version);
    }

    private static MaterialEntity MapToEntity(MaterialFormulation domain)
    {
        return new MaterialEntity
        {
            MaterialId = domain.MaterialId,
            RecordType = "MATERIAL",
            Name = domain.Name,
            Density = domain.Density is null ? null : new DensityEntity
            {
                Value = domain.Density.Value,
                Unit = domain.Density.Unit
            },
            Formulation = domain.Formulation.Select(f => new FormulationComponentEntity
            {
                Component = f.Component,
                Percentage = f.Percentage,
                Type = f.Type
            }).ToList(),
            Properties = domain.Properties,
            Version = domain.Version
        };
    }

    private static Result<Dictionary<string, AttributeValue>?> ParseCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return Result.Success<Dictionary<string, AttributeValue>?>(null);
        }

        try
        {
            var json = Cursor.FromBase64(cursor);
            var doc = Document.FromJson(json);
            return Result.Success<Dictionary<string, AttributeValue>?>(doc.ToAttributeMap());
        }
        catch
        {
            return Result.Failure<Dictionary<string, AttributeValue>?>(new Error("Pagination.InvalidCursor", "The provided cursor is invalid."));
        }
    }

    private static string? GetNextCursor(Dictionary<string, AttributeValue>? lastEvaluatedKey)
    {
        if (lastEvaluatedKey == null || lastEvaluatedKey.Count == 0)
        {
            return null;
        }

        var doc = Document.FromAttributeMap(lastEvaluatedKey);
        return Cursor.ToBase64(doc.ToJson());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetAllAsync] Listing materials (Limit: {limit}, Cursor: {cursor})")]
    private partial void LogListingMaterials(int limit, string? cursor);

    [LoggerMessage(Level = LogLevel.Error, Message = "[GetAllAsync] Failed to list materials: {error}")]
    private partial void LogListingMaterialsError(string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetCountAsync] Count: {count}")]
    private partial void LogCount(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "[GetUniqueComponentsAsync] Found {count} unique components")]
    private partial void LogUniqueComponents(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[SaveAsync] Saving material {materialId}")]
    private partial void LogSavingMaterial(string materialId);
}

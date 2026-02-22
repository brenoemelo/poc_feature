using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using PoC.Materials.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;
using System.Text;

namespace PoC.Materials.Infrastructure.Persistence;

public sealed class DynamoDbMaterialRepository(IDynamoDBContext context, IAmazonDynamoDB client, IOptions<MaterialsOptions> options) : IMaterialRepository
{
    private readonly MaterialsOptions _options = options.Value;
    private readonly DynamoDBOperationConfig _dynamoConfig = new() { OverrideTableName = options.Value.TableName };

    public async Task<Result<PagedResult<MaterialFormulation>>> GetAllAsync(int limit, string? cursor)
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

            if (!string.IsNullOrEmpty(cursor))
            {
                try
                {
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                    var doc = Document.FromJson(json);
                    request.ExclusiveStartKey = doc.ToAttributeMap();
                }
                catch
                {
                    return Result.Failure<PagedResult<MaterialFormulation>>(new Error("Pagination.InvalidCursor", "The provided cursor is invalid."));
                }
            }

            var response = await client.QueryAsync(request);
            var items = new List<MaterialFormulation>();
            var dynamoItems = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            items.AddRange(dynamoItems
                .Select(item => context.FromDocument<MaterialEntity>(Document.FromAttributeMap(item)))
                .Where(entity => entity is not null)
                .Select(MapToDomain));

            string? nextCursor = null;
            if (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0)
            {
                var lastKeyDoc = Document.FromAttributeMap(response.LastEvaluatedKey);
                var json = lastKeyDoc.ToJson();
                nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            }

            return Result.Success(new PagedResult<MaterialFormulation>(items, nextCursor));
        }
        catch (Exception ex)
        {
            return Result.Failure<PagedResult<MaterialFormulation>>(new Error("DynamoDb.Error", ex.Message));
        }
    }

    public async Task<Result<MaterialFormulation>> GetByIdAsync(string materialId)
    {
        try
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var entity = await context.LoadAsync<MaterialEntity>(materialId, _dynamoConfig);
#pragma warning restore CS0618 // Type or member is obsolete
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

            if (!string.IsNullOrEmpty(cursor))
            {
                try
                {
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                    var doc = Document.FromJson(json);
                    request.ExclusiveStartKey = doc.ToAttributeMap();
                }
                catch
                {
                    return Result.Failure<PagedResult<string>>(new Error("Pagination.InvalidCursor", "The provided cursor is invalid."));
                }
            }

            var response = await client.QueryAsync(request);
            var uniqueComponents = new HashSet<string>();
            var dynamoItems = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            foreach (var item in dynamoItems)
            {
                var doc = Document.FromAttributeMap(item);
                var entity = context.FromDocument<MaterialEntity>(doc);
                
                if (entity.Formulation != null)
                {
                    var components = entity.Formulation
                        .Select(f => f.Component)
                        .Where(c => !string.IsNullOrWhiteSpace(c));
                    uniqueComponents.UnionWith(components);
                }
            }
            
            string? nextCursor = null;
            if (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0)
            {
                var lastKeyDoc = Document.FromAttributeMap(response.LastEvaluatedKey);
                var json = lastKeyDoc.ToJson();
                nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            }

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
            var entity = MapToEntity(material);
#pragma warning disable CS0618 // Type or member is obsolete
            await context.SaveAsync(entity, _dynamoConfig);
#pragma warning restore CS0618 // Type or member is obsolete
            return Result.Success();
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
#pragma warning disable CS0618 // Type or member is obsolete
            await context.DeleteAsync<MaterialEntity>(materialId, _dynamoConfig);
#pragma warning restore CS0618 // Type or member is obsolete
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
}

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using PoC.Materials.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;
using System.Text;

namespace PoC.Materials.Infrastructure.Persistence;

public sealed class DynamoDbMaterialRepository(IDynamoDBContext context, IAmazonDynamoDB client) : IMaterialRepository
{
    public async Task<Result<PagedResult<MaterialFormulation>>> GetAllAsync(int limit, string? cursor)
    {
        try
        {
            var tableName = Environment.GetEnvironmentVariable("MATERIALS_TABLE_NAME") ?? "materials-table";
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
            foreach (var item in response.Items)
            {
                var doc = Document.FromAttributeMap(item);
                var entity = context.FromDocument<MaterialEntity>(doc);
                items.Add(MapToDomain(entity));
            }

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
            var entity = await context.LoadAsync<MaterialEntity>(materialId);
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
            var tableName = Environment.GetEnvironmentVariable("MATERIALS_TABLE_NAME") ?? "materials-table";
            var response = await client.DescribeTableAsync(tableName);
            
            // ItemCount is approximate, updated every 6 hours. 
            // For a PoC/High-scale system, this is often preferred over scanning.
            // If realtime count is needed, we should maintain a counter item.
            var count = (int)response.Table.ItemCount;
            return Result.Success(count);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(new Error("DynamoDb.Error", ex.Message));
        }
    }

    public async Task<Result> SaveAsync(MaterialFormulation material)
    {
        try
        {
            // Optimistic Locking: We trust DynamoDBContext to handle the Version check.
            // We do NOT manually copy the version unless we want to force an overwrite (which we don't).
            var entity = MapToEntity(material);
            await context.SaveAsync(entity);
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
            await context.DeleteAsync<MaterialEntity>(materialId);
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

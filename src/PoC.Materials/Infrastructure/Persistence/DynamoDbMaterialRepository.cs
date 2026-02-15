using Amazon.DynamoDBv2.DataModel;
using PoC.Materials.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Materials.Infrastructure.Persistence;

public sealed class DynamoDbMaterialRepository(IDynamoDBContext context) : IMaterialRepository
{
    public async Task<Result<IEnumerable<MaterialFormulation>>> GetAllAsync()
    {
        try
        {
            var scan = context.ScanAsync<MaterialEntity>(default);
            var entities = await scan.GetRemainingAsync();
            return Result.Success(entities.Select(MapToDomain));
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<MaterialFormulation>>(new Error("DynamoDb.Error", ex.Message));
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

    public async Task<Result> SaveAsync(MaterialFormulation material)
    {
        try
        {
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
        return new MaterialFormulation
        {
            MaterialId = entity.MaterialId,
            Name = entity.Name,
            Density = entity.Density is null ? null : new Density
            {
                Value = entity.Density.Value,
                Unit = entity.Density.Unit
            },
            Formulation = entity.Formulation.Select(f => new FormulationComponent
            {
                Component = f.Component,
                Percentage = f.Percentage,
                Type = f.Type
            }).ToList(),
            Properties = entity.Properties
        };
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
            Properties = domain.Properties
        };
    }
}

using Amazon.DynamoDBv2.DataModel;
using PoC.Shared.Models;

namespace PoC.Materials.Repositories;

public sealed class DynamoDbMaterialRepository(IDynamoDBContext context) : IMaterialRepository
{
    public async Task<IEnumerable<MaterialFormulation>> GetAllAsync()
    {
        var scan = context.ScanAsync<MaterialEntity>(default);
        var entities = await scan.GetRemainingAsync();
        return entities.Select(MapToDomain);
    }

    public async Task<MaterialFormulation?> GetByIdAsync(string materialId)
    {
        var entity = await context.LoadAsync<MaterialEntity>(materialId);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task SaveAsync(MaterialFormulation material)
    {
        var entity = MapToEntity(material);
        await context.SaveAsync(entity);
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

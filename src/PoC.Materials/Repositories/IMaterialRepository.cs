using PoC.Shared.Models;

namespace PoC.Materials.Repositories;

public interface IMaterialRepository
{
    Task<IEnumerable<MaterialFormulation>> GetAllAsync();
    Task<MaterialFormulation?> GetByIdAsync(string materialId);
    Task SaveAsync(MaterialFormulation material);
}

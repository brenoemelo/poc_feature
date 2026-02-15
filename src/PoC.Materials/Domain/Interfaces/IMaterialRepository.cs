using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Materials.Domain.Interfaces;

public interface IMaterialRepository
{
    Task<Result<IEnumerable<MaterialFormulation>>> GetAllAsync();
    Task<Result<MaterialFormulation>> GetByIdAsync(string materialId);
    Task<Result> SaveAsync(MaterialFormulation material);
    Task<Result> DeleteAsync(string materialId);
}

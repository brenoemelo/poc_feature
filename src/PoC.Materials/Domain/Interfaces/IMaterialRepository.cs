using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Materials.Domain.Interfaces;

public interface IMaterialRepository
{
    Task<Result<PagedResult<MaterialFormulation>>> GetAllAsync(int limit, string? cursor);
    Task<Result<MaterialFormulation>> GetByIdAsync(string materialId);
    Task<Result<int>> GetCountAsync();
    Task<Result<PagedResult<string>>> GetUniqueComponentsAsync(int limit, string? cursor);
    Task<Result> SaveAsync(MaterialFormulation material);
    Task<Result> DeleteAsync(string materialId);
}

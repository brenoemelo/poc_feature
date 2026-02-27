using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Materials.Domain.Interfaces;

public interface IMaterialRepository
{
    Task<Result<PagedResult<MaterialFormulation>>> GetAllAsync(int limit, string? cursor);
    Task<Result<MaterialFormulation>> GetByIdAsync(string materialId);
    Task<Result<int>> GetCountAsync();
    /// <summary>
    /// Scans materials to find components.
    /// Note: The limit applies to the number of materials scanned, not the number of components returned.
    /// </summary>
    Task<Result<PagedResult<string>>> ScanComponentsAsync(int materialLimit, string? cursor);
    Task<Result> SaveAsync(MaterialFormulation material);
    Task<Result> DeleteAsync(string materialId);
}

using PoC.Shared.Models;

namespace PoC.Costing.Domain.Interfaces;

public interface IMaterialsClient
{
    Task<IEnumerable<MaterialFormulation>> GetAllMaterialsAsync();
}

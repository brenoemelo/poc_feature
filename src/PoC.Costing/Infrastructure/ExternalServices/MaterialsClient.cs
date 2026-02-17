using PoC.Costing.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Costing.Infrastructure.ExternalServices;

public sealed class MaterialsClient(HttpClient httpClient, ILogger<MaterialsClient> logger) : IMaterialsClient
{
    public async Task<IEnumerable<MaterialFormulation>> GetAllMaterialsAsync()
    {
        var allMaterials = new List<MaterialFormulation>();
        string? cursor = null;

        do
        {
            var url = $"api/v1/materials?limit=100" + (cursor != null ? $"&cursor={cursor}" : string.Empty);
            
            try 
            {
                var response = await httpClient.GetFromJsonAsync<PagedResponse<MaterialFormulation>>(url);
                
                if (response?.Data != null)
                {
                    allMaterials.AddRange(response.Data);
                    cursor = response.Meta?.NextCursor;
                }
                else
                {
                    cursor = null;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching materials from Materials Service");
                throw;
            }
        }
        while (!string.IsNullOrEmpty(cursor));

        return allMaterials;
    }
}

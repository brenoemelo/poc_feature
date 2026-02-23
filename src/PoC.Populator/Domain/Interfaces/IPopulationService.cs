using PoC.Shared.Common;
using PoC.Populator.Domain.Models;
using PoC.Shared.Models;

namespace PoC.Populator.Domain.Interfaces;

public interface IPopulationService
{
    Task<Result<PopulationJobResponse>> CreateJobAsync(PopulationRequest request);
}

public record PopulationJobResponse(string Message, int TotalRecords, int BatchesQueued);

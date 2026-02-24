using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Populator.Domain.Interfaces;

public interface IPopulationService
{
    Task<Result<PopulationJobResponse>> CreateJobAsync(PopulationRequest request);
}

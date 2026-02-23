using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PoC.FeatureFlags.Extensions;
using PoC.Populator.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Extensions;
using PoC.Populator.Domain.Models;
using PoC.Populator.Domain.Validators;
using PoC.Shared.Models;

namespace PoC.Populator.API.Endpoints;

public static partial class PopulatorEndpoints
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing population request for {Count} items.")]
    private static partial void LogProcessingRequest(ILogger logger, int count);

    public static RouteGroupBuilder MapPopulatorEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/jobs", HandlePopulationRequestAsync)
             .WithName("CreatePopulationJob")
             .WithFeatureGate("population-jobs");

        return group;
    }

    private static async Task<IResult> HandlePopulationRequestAsync(
        [FromBody] PopulationRequest request,
        [FromServices] IValidator<PopulationRequest> validator,
        [FromServices] IPopulationService populationService,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        [FromServices] ILogger<Program> logger)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        LogProcessingRequest(logger, request.Count);

        var result = await populationService.CreateJobAsync(request);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        var selfUrl = linkGenerator.GetUriByName(httpContext, "CreatePopulationJob") ?? "/api/v1/populator/jobs";

        var response = new ApiResponse<PopulationJobResponse>(
            result.Value,
            [new Link("self", selfUrl, "POST")]);

        return Results.Accepted(uri: selfUrl, value: response);
    }
}

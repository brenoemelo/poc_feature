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
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing population request for {Count} items. Min: {Min}, Max: {Max}")]
    private static partial void LogProcessingRequest(ILogger logger, int count, int? min, int? max);

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
        Console.WriteLine($"[PopulatorEndpoints] Processing request: Count={request.Count}, Min={request.MinComponents}, Max={request.MaxComponents}");
        LogProcessingRequest(logger, request.Count, request.MinComponents, request.MaxComponents);

        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("Validation failed: {Errors}", string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

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

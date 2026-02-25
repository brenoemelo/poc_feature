using FluentValidation;
using PoC.FeatureFlags.Extensions;
using PoC.Materials.API.Extensions;
using PoC.Materials.Domain.Interfaces;
using PoC.Materials.Infrastructure;
using PoC.Shared.Common;
using PoC.Shared.Extensions;
using PoC.Shared.Models;
using System.Diagnostics;

namespace PoC.Materials.API.Endpoints;

public static partial class MaterialsEndpoints
{
    private static readonly ActivitySource ActivitySource = new("PoC-Materials");

    public static RouteGroupBuilder MapMaterialsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllMaterialsAsync)
             .WithName("GetMaterials")
             .WithFeatureGate("materials-crud")
             .Produces<PagedResponse<MaterialFormulation>>();

        group.MapGet("/count", GetMaterialCountAsync)
             .WithName("GetMaterialCount")
             .WithFeatureGate("materials-crud")
             .Produces<ApiResponse<object>>();

        group.MapGet("/components", GetUniqueComponentsAsync)
             .WithName("GetUniqueComponents")
             .WithFeatureGate("view-all-components")
             .Produces<PagedResponse<string>>();

        group.MapGet("/{id}", GetMaterialByIdAsync)
             .WithName("GetMaterialById")
             .WithFeatureGate("materials-crud")
             .Produces<ApiResponse<MaterialFormulation>>()
             .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateMaterialAsync)
             .WithName("CreateMaterial")
             .WithFeatureGate("materials-crud")
             .Produces<ApiResponse<MaterialFormulation>>(StatusCodes.Status201Created)
             .Produces(StatusCodes.Status400BadRequest);

        group.MapDelete("/{id}", DeleteMaterialAsync)
             .WithName("DeleteMaterial")
             .WithFeatureGate("materials-crud")
             .Produces(StatusCodes.Status204NoContent)
             .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialQuery] Listing materials (Limit: {limit}, Cursor: {cursor})")]
    private static partial void LogListingMaterials(ILogger logger, int limit, string? cursor);

    [LoggerMessage(Level = LogLevel.Error, Message = "[MaterialQuery] Failed to list materials: {error} - {detail}")]
    private static partial void LogListingMaterialsFailed(ILogger logger, string error, string detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialQuery] Counting materials")]
    private static partial void LogCountingMaterials(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialQuery] Retrieving unique components (Limit: {limit}, Cursor: {cursor})")]
    private static partial void LogRetrievingComponents(ILogger logger, int limit, string? cursor);

    [LoggerMessage(Level = LogLevel.Error, Message = "[MaterialQuery] Failed to retrieve components: {error} - {detail}")]
    private static partial void LogRetrievingComponentsFailed(ILogger logger, string error, string detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialQuery] Getting material {materialId}")]
    private static partial void LogGettingMaterial(ILogger logger, string materialId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialQuery] Failed to get material {materialId}: {error} - {detail}")]
    private static partial void LogGettingMaterialFailed(ILogger logger, string materialId, string error, string detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialCreation] Processing material: {name} ({materialId})")]
    private static partial void LogProcessingMaterial(ILogger logger, string name, string materialId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialCreation] Validation failed for {materialId}")]
    private static partial void LogValidationFailed(ILogger logger, string materialId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialCreation] Successfully saved material {materialId}")]
    private static partial void LogMaterialSaved(ILogger logger, string materialId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialDeletion] Deletion requested for {materialId}")]
    private static partial void LogDeletionRequested(ILogger logger, string materialId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialDeletion] Material {materialId} not found")]
    private static partial void LogMaterialNotFound(ILogger logger, string materialId);

    private static async Task<IResult> GetAllMaterialsAsync(
        IMaterialRepository repository,
        ILogger<Program> logger,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        int limit = 10,
        string? cursor = null)
    {
        using var activity = ActivitySource.StartActivity("Manual.GetAllMaterials");
        activity?.SetTag("manual.trace", "true");
        activity?.SetTag("http.limit", limit);

        LogListingMaterials(logger, limit, cursor);
        var result = await repository.GetAllAsync(limit, cursor);
        
        if (result.IsFailure)
        {
            LogListingMaterialsFailed(logger, result.Error.Code, result.Error.Description);
            return result.ToProblem();
        }

        var response = result.Value.ToPagedResponse(httpContext, linkGenerator, "GetMaterials", limit, cursor);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetMaterialCountAsync(
        IMaterialRepository repository,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        ILogger<Program> logger)
    {
        LogCountingMaterials(logger);
        var result = await repository.GetCountAsync();

        if (result.IsFailure)
            return result.ToProblem();

        var selfUrl = linkGenerator.GetUriByName(httpContext, "GetMaterialCount") ?? "/api/v1/materials/count";
        var response = new ApiResponse<object>(
            new { count = result.Value },
            [new Link("self", selfUrl, "GET")]);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetUniqueComponentsAsync(
        IMaterialRepository repository,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        ILogger<Program> logger,
        int limit = 10,
        string? cursor = null)
    {
        LogRetrievingComponents(logger, limit, cursor);
        var result = await repository.GetUniqueComponentsAsync(limit, cursor);

        if (result.IsFailure)
        {
            LogRetrievingComponentsFailed(logger, result.Error.Code, result.Error.Description);
            return result.ToProblem();
        }

        var response = result.Value.ToPagedResponse(httpContext, linkGenerator, "GetUniqueComponents", limit, cursor);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetMaterialByIdAsync(
        string id,
        IMaterialRepository repository,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        ILogger<Program> logger)
    {
        LogGettingMaterial(logger, id);
        var result = await repository.GetByIdAsync(id);

        if (result.IsFailure)
        {
            LogGettingMaterialFailed(logger, id, result.Error.Code, result.Error.Description);
            return result.ToProblem();
        }

        var selfUrl = linkGenerator.GetUriByName(httpContext, "GetMaterialById", new { id }) ?? $"/api/v1/materials/{id}";
        var response = new ApiResponse<MaterialFormulation>(
            result.Value,
            [
                new Link("self", selfUrl, "GET"),
                new Link("delete", selfUrl, "DELETE")
            ]);

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateMaterialAsync(
        MaterialFormulation input,
        IMaterialRepository repository,
        IValidator<MaterialFormulation> validator,
        MaterialsMetrics metrics,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        ILogger<Program> logger)
    {
        LogProcessingMaterial(logger, input.Name, input.MaterialId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var validationResult = await validator.ValidateAsync(input);
        if (!validationResult.IsValid)
        {
            LogValidationFailed(logger, input.MaterialId);
            metrics.RecordIngestion("validation_failed");
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var result = await repository.SaveAsync(input);
        
        stopwatch.Stop();
        metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds);

        if (result.IsFailure)
        {
             metrics.RecordIngestion("failed");
             return result.ToProblem();
        }

        metrics.RecordIngestion("success");
        LogMaterialSaved(logger, input.MaterialId);

        var selfUrl = linkGenerator.GetUriByName(httpContext, "GetMaterialById", new { id = input.MaterialId })
                      ?? $"/api/v1/materials/{input.MaterialId}";

        var response = new ApiResponse<MaterialFormulation>(
            input,
            [new Link("self", selfUrl, "GET")]);

        return Results.Created(selfUrl, response);
    }
 
    private static async Task<IResult> DeleteMaterialAsync(
        string id,
        IMaterialRepository repository,
        ILogger<Program> logger)
    {
        LogDeletionRequested(logger, id);
 
        var existingResult = await repository.GetByIdAsync(id);
        if (existingResult.IsFailure)
        {
            LogMaterialNotFound(logger, id);
            return existingResult.ToProblem();
        }

        var deleteResult = await repository.DeleteAsync(id);
        if (deleteResult.IsFailure)
        {
             return deleteResult.ToProblem();
        }

        return Results.NoContent();
    }
}

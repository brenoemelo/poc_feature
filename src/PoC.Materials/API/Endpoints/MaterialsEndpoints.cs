using FluentValidation;
using PoC.Materials.API.Extensions;
using PoC.Materials.Domain.Interfaces;

using PoC.Shared.Common;
using PoC.Shared.Infrastructure.Extensions;
using PoC.Shared.Models;

namespace PoC.Materials.API.Endpoints;

public static class MaterialsEndpoints
{
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

    private static async Task<IResult> GetAllMaterialsAsync(
        IMaterialRepository repository,
        ILogger<Program> logger,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        int limit = 10,
        string? cursor = null)
    {
        logger.LogInformation("[MaterialQuery] Listing materials (Limit: {Limit}, Cursor: {Cursor})", limit, cursor);
        var result = await repository.GetAllAsync(limit, cursor);
        
        if (result.IsFailure)
        {
            logger.LogError("[MaterialQuery] Failed to list materials: {Error} - {Detail}", result.Error.Code, result.Error.Description);
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
        logger.LogInformation("[MaterialQuery] Counting materials");
        var result = await repository.GetCountAsync();

        if (result.IsFailure)
            return result.ToProblem();

        var selfUrl = linkGenerator.GetUriByName(httpContext, "GetMaterialCount") ?? "/api/v1/materials/count";
        var response = new ApiResponse<object>(
            new { count = result.Value },
            [new Link("self", selfUrl, "GET")]);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetMaterialByIdAsync(
        string id,
        IMaterialRepository repository,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        ILogger<Program> logger)
    {
        logger.LogInformation("[MaterialQuery] Getting material {MaterialId}", id);
        var result = await repository.GetByIdAsync(id);

        if (result.IsFailure)
            return result.ToProblem();

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
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        ILogger<Program> logger)
    {
        logger.LogInformation(
            "[MaterialCreation] Processing material: {Name} ({MaterialId})",
            input.Name,
            input.MaterialId);

        var validationResult = await validator.ValidateAsync(input);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("[MaterialCreation] Validation failed for {MaterialId}", input.MaterialId);
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var result = await repository.SaveAsync(input);
        
        if (result.IsFailure)
        {
             return result.ToProblem();
        }

        logger.LogInformation("[MaterialCreation] Successfully saved material {MaterialId}", input.MaterialId);

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
        logger.LogInformation("[MaterialDeletion] Deletion requested for {MaterialId}", id);
 
        var existingResult = await repository.GetByIdAsync(id);
        if (existingResult.IsFailure)
        {
            logger.LogWarning("[MaterialDeletion] Material {MaterialId} not found", id);
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

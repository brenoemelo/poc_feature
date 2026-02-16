using FluentValidation;
using PoC.Materials.API.Extensions;
using PoC.Materials.Domain.Interfaces;
using PoC.Shared.API;
using PoC.Shared.Common;
using PoC.Shared.Models;

namespace PoC.Materials.API.Endpoints;

public static class MaterialsEndpoints
{
    public static RouteGroupBuilder MapMaterialsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllMaterialsAsync)
             .WithName("GetMaterials")
             .Produces<PagedResponse<MaterialFormulation>>();

        group.MapGet("/count", GetMaterialCountAsync)
             .WithName("GetMaterialCount")
             .Produces<int>();

        group.MapGet("/{id}", GetMaterialByIdAsync)
             .WithName("GetMaterialById")
             .Produces<MaterialFormulation>()
             .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateMaterialAsync)
             .WithName("CreateMaterial")
             .Produces<MaterialFormulation>(StatusCodes.Status201Created)
             .Produces(StatusCodes.Status400BadRequest);
 
        group.MapDelete("/{id}", DeleteMaterialAsync)
             .WithName("DeleteMaterial")
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
        ILogger<Program> logger)
    {
        logger.LogInformation("[MaterialQuery] Counting materials");
        var result = await repository.GetCountAsync();

        return result.IsSuccess
            ? Results.Ok(new { count = result.Value })
            : result.ToProblem();
    }

    private static async Task<IResult> GetMaterialByIdAsync(
        string id,
        IMaterialRepository repository,
        ILogger<Program> logger)
    {
        logger.LogInformation("[MaterialQuery] Getting material {MaterialId}", id);
        var result = await repository.GetByIdAsync(id);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.ToProblem();
    }

    private static async Task<IResult> CreateMaterialAsync(
        MaterialFormulation input,
        IMaterialRepository repository,
        IValidator<MaterialFormulation> validator,
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
            logger.LogWarning("[MaterialCreation] Validation failed for {MaterialId}", input.MaterialId);
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var result = await repository.SaveAsync(input);
        
        if (result.IsFailure)
        {
             return result.ToProblem();
        }

        logger.LogInformation("[MaterialCreation] Successfully saved material {MaterialId}", input.MaterialId);

        return Results.Created($"/materials/{input.MaterialId}", input);
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

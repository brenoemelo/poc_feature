using FluentValidation;
using PoC.Materials.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;
using PoC.Shared.Validation;

namespace PoC.Materials.API.Endpoints;

public static class MaterialsEndpoints
{
    public static RouteGroupBuilder MapMaterialsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllMaterialsAsync)
             .WithName("GetMaterials")
             .Produces<IEnumerable<MaterialFormulation>>();

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
        ILogger<Program> logger)
    {
        logger.LogInformation("[MaterialQuery] Listing all materials");
        var result = await repository.GetAllAsync();
        
        return result.IsSuccess 
            ? Results.Ok(result.Value) 
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
            var problemDetails = validationResult.ToProblemDetails();
            return Results.Problem(
                title: problemDetails.Title,
                detail: string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)),
                statusCode: StatusCodes.Status400BadRequest);
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

    private static IResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert success result to problem.");
        }

        var error = result.Error;

        if (error == Error.NotFound)
        {
            return Results.Problem(
                title: "Resource not found",
                detail: error.Description,
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Problem(
            title: "An error occurred",
            detail: error.Description,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}

using FluentValidation;
using PoC.Materials.Repositories;
using PoC.Shared.Models;
using PoC.Shared.Validation;

namespace PoC.Materials.Endpoints;

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

        return group;
    }

    private static async Task<IResult> GetAllMaterialsAsync(
        IMaterialRepository repository,
        ILogger<Program> logger)
    {
        logger.LogInformation("[MaterialQuery] Listing all materials");
        var materials = await repository.GetAllAsync();
        return Results.Ok(materials);
    }

    private static async Task<IResult> GetMaterialByIdAsync(
        string id,
        IMaterialRepository repository,
        ILogger<Program> logger)
    {
        logger.LogInformation("[MaterialQuery] Getting material {MaterialId}", id);
        var material = await repository.GetByIdAsync(id);

        return material is not null
            ? Results.Ok(material)
            : Results.Problem(
                title: "Material not found",
                detail: $"Material with ID '{id}' does not exist",
                statusCode: StatusCodes.Status404NotFound);
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

        await repository.SaveAsync(input);
        logger.LogInformation("[MaterialCreation] Successfully saved material {MaterialId}", input.MaterialId);

        return Results.Created($"/materials/{input.MaterialId}", input);
    }
}

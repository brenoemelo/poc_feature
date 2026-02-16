using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Domain.Services;
using PoC.Shared.API;
using PoC.Shared.Infrastructure.Extensions;
using PoC.Shared.Models;

namespace PoC.Costing.API.Endpoints;

public static class CostingEndpoints
{
    public static RouteGroupBuilder MapCostingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/prices", UpsertPriceAsync)
             .WithName("UpsertPrice")
             .WithFeatureGate("price-ingestion");

        group.MapPost("/estimations", CalculateCostAsync)
             .WithName("CalculateCost")
             .WithFeatureGate("price-calculation");

        group.MapGet("/estimations/batch", CalculateAllCostsAsync)
             .WithName("CalculateAllCosts");

        return group;
    }

    private static async Task<IResult> UpsertPriceAsync(
        [FromBody] ComponentPriceRequest request,
        [FromServices] ICostingRepository repository,
        [FromServices] IValidator<ComponentPriceRequest> validator,
        [FromServices] ILogger<Program> logger)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        logger.LogInformation("Upserting price for component: {ComponentName}", request.ComponentName);
        var result = await repository.UpsertPriceAsync(request);

        return result.IsSuccess
            ? Results.Ok(new { message = "Price updated successfully" })
            : result.ToProblem();
    }

    private static async Task<IResult> CalculateCostAsync(
        [FromBody] CostCalculationRequest request,
        [FromServices] ICostingRepository repository,
        [FromServices] ICostCalculator costCalculator,
        [FromServices] IValidator<CostCalculationRequest> validator,
        [FromServices] ILogger<Program> logger)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        logger.LogInformation("Calculating cost for material: {MaterialId}", request.MaterialId);

        var componentNames = request.Formulation.Select(f => f.Component).Distinct().ToList();
        var pricesResult = await repository.GetPricesAsync(componentNames);
        
        if (pricesResult.IsFailure)
        {
            return pricesResult.ToProblem();
        }

        var result = costCalculator.Calculate(
            request.MaterialId, 
            request.Formulation, 
            request.DesiredMarginPercent, 
            pricesResult.Value);

        return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> CalculateAllCostsAsync(
        [FromServices] IMaterialsClient materialsClient,
        [FromServices] ICostCalculator costCalculator,
        [FromServices] ICostingRepository repository,
        [FromServices] ILogger<Program> logger)
    {
        logger.LogInformation("Starting bulk cost calculation for all materials");
        
        IEnumerable<MaterialFormulation> materials;
        try
        {
            materials = await materialsClient.GetAllMaterialsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch materials");
            return Results.Problem("Failed to fetch materials from Materials Service", statusCode: 502);
        }

        if (!materials.Any())
        {
            return Results.Ok(new List<CostCalculationResponse>());
        }

        var uniqueComponents = materials.SelectMany(m => m.Formulation).Select(f => f.Component).Distinct().ToList();
        var pricesResult = await repository.GetPricesAsync(uniqueComponents);
        
        if (pricesResult.IsFailure)
        {
             return pricesResult.ToProblem();
        }

        var prices = pricesResult.Value;

        var results = new List<CostCalculationResponse>();

        foreach (var material in materials)
        {
            var formulationInput = material.Formulation
                .Select(f => new FormulationInput(f.Component, f.Percentage))
                .ToList();
            
            var calculationResult = costCalculator.Calculate(material.MaterialId, formulationInput, null, prices);
            
            if (calculationResult.IsSuccess)
            {
                results.Add(calculationResult.Value);
            }
            else
            {
                logger.LogWarning("Skipping material {MaterialId}: {Error}", material.MaterialId, calculationResult.Error.Description);
            }
        }

        return Results.Ok(results);
    }
}

using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Domain.Services;

using PoC.FeatureFlags.Extensions;
using PoC.Shared.Common;
using PoC.Shared.Extensions;
using PoC.Shared.Models;

namespace PoC.Costing.API.Endpoints;

public static partial class CostingEndpoints
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Upserting price for component: {componentName}")]
    private static partial void LogUpsertingPrice(ILogger logger, string componentName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retrieving all component prices")]
    private static partial void LogRetrievingAllPrices(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retrieving count of component prices")]
    private static partial void LogRetrievingPricesCount(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Calculating cost for material: {materialId}")]
    private static partial void LogCalculatingCost(ILogger logger, string materialId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Calculation success. TotalCost: {totalCost}, BreakdownCount: {count}")]
    private static partial void LogCalculationSuccess(ILogger logger, decimal totalCost, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting bulk cost calculation for all materials")]
    private static partial void LogStartingBulkCalculation(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch materials")]
    private static partial void LogFailedToFetchMaterials(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping material {materialId}: {error}")]
    private static partial void LogSkippingMaterial(ILogger logger, string materialId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Batch cost calculation completed. Processed {count} materials")]
    private static partial void LogBatchCalculationCompleted(ILogger logger, int count);

    public static RouteGroupBuilder MapCostingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/prices", UpsertPriceAsync)
             .WithName("UpsertPrice")
             .WithFeatureGate("price-ingestion");

        group.MapGet("/prices", GetAllPricesAsync)
             .WithName("GetAllPrices")
             .WithFeatureGate("view-all-prices");

        group.MapGet("/prices/count", GetPricesCountAsync)
             .WithName("GetPricesCount")
             .WithFeatureGate("count-all-prices");

        group.MapPost("/estimations", CalculateCostAsync)
             .WithName("CalculateCost")
             .WithFeatureGate("price-calculation");

        group.MapGet("/estimations/batch", CalculateAllCostsAsync)
             .WithName("CalculateAllCosts")
             .WithFeatureGate("costing-batch");

        return group;
    }

    private static async Task<IResult> UpsertPriceAsync(
        [FromBody] ComponentPriceRequest request,
        [FromServices] ICostingRepository repository,
        [FromServices] IValidator<ComponentPriceRequest> validator,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        [FromServices] ILogger<Program> logger)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        LogUpsertingPrice(logger, request.ComponentName);
        var result = await repository.UpsertPriceAsync(request);

        if (result.IsFailure)
            return result.ToProblem();

        var selfUrl = linkGenerator.GetUriByName(httpContext, "UpsertPrice") ?? "/api/v1/costing/prices";
        var response = new ApiResponse<object>(
            new { message = "Price updated successfully", component_name = request.ComponentName },
            [new Link("self", selfUrl, "POST")]);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetAllPricesAsync(
        [FromServices] ICostingRepository repository,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        [FromServices] ILogger<Program> logger)
    {
        LogRetrievingAllPrices(logger);
        var result = await repository.GetAllPricesAsync();

        if (result.IsFailure)
            return result.ToProblem();

        var selfUrl = linkGenerator.GetUriByName(httpContext, "GetAllPrices") ?? "/api/v1/costing/prices";
        var response = new ApiResponse<IEnumerable<ComponentPriceResponse>>(
            result.Value,
            [new Link("self", selfUrl, "GET")]);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetPricesCountAsync(
        [FromServices] ICostingRepository repository,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        [FromServices] ILogger<Program> logger)
    {
        LogRetrievingPricesCount(logger);
        var result = await repository.GetPricesCountAsync();

        if (result.IsFailure)
            return result.ToProblem();

        var selfUrl = linkGenerator.GetUriByName(httpContext, "GetPricesCount") ?? "/api/v1/costing/prices/count";
        var response = new ApiResponse<object>(
            new { count = result.Value },
            [new Link("self", selfUrl, "GET")]);

        return Results.Ok(response);
    }

    private static async Task<IResult> CalculateCostAsync(
        [FromBody] CostCalculationRequest request,
        [FromServices] ICostingRepository repository,
        [FromServices] ICostCalculator costCalculator,
        [FromServices] PoC.Costing.Infrastructure.BusinessMetrics metrics,
        [FromServices] IValidator<CostCalculationRequest> validator,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        [FromServices] ILogger<Program> logger)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        LogCalculatingCost(logger, request.MaterialId);

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

        if (result.IsFailure)
            return result.ToProblem();

        var selfUrl = linkGenerator.GetUriByName(httpContext, "CalculateCost") ?? "/api/v1/costing/estimations";
        var response = new ApiResponse<CostCalculationResponse>(
            result.Value,
            [new Link("self", selfUrl, "POST")]);
            
        LogCalculationSuccess(logger, result.Value.TotalCost, result.Value.Breakdown?.Count ?? 0);
        metrics.RecordCalculation((double)result.Value.TotalCost);

        return Results.Ok(response);
    }

    private static async Task<IResult> CalculateAllCostsAsync(
        [FromServices] IMaterialsClient materialsClient,
        [FromServices] ICostCalculator costCalculator,
        [FromServices] ICostingRepository repository,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        [FromServices] ILogger<Program> logger)
    {
        LogStartingBulkCalculation(logger);
        
        IEnumerable<MaterialFormulation> materials;
        try
        {
            materials = await materialsClient.GetAllMaterialsAsync();
        }
        catch (Exception ex)
        {
            LogFailedToFetchMaterials(logger, ex);
            return Results.Problem("Failed to fetch materials from Materials Service", statusCode: 502);
        }

        var selfUrl = linkGenerator.GetUriByName(httpContext, "CalculateAllCosts") ?? "/api/v1/costing/estimations/batch";

        if (!materials.Any())
        {
            var emptyResponse = new ApiResponse<IReadOnlyList<CostCalculationResponse>>(
                [],
                [new Link("self", selfUrl, "GET")]);
            return Results.Ok(emptyResponse);
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
                LogSkippingMaterial(logger, material.MaterialId, calculationResult.Error.Description);
            }
        }

        var response = new ApiResponse<IReadOnlyList<CostCalculationResponse>>(
            results,
            [new Link("self", selfUrl, "GET")]);

        LogBatchCalculationCompleted(logger, results.Count);

        return Results.Ok(response);
    }
}

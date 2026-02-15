using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PoC.Costing.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;
using PoC.Shared.Validation;

namespace PoC.Costing.API.Endpoints;

public static class CostingEndpoints
{
    public static RouteGroupBuilder MapCostingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/prices", UpsertPriceAsync)
             .WithName("UpsertPrice");

        group.MapPost("/calculate-cost", CalculateCostAsync)
             .WithName("CalculateCost");

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
            var problemDetails = validationResult.ToProblemDetails();
            return Results.Problem(
                title: problemDetails.Title,
                detail: string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)),
                statusCode: StatusCodes.Status400BadRequest);
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
        [FromServices] IValidator<CostCalculationRequest> validator,
        [FromServices] ILogger<Program> logger)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var problemDetails = validationResult.ToProblemDetails();
            return Results.Problem(
                title: problemDetails.Title,
                detail: string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        logger.LogInformation("Calculating cost for material: {MaterialId}", request.MaterialId);

        var componentNames = request.Formulation.Select(f => f.Component).Distinct().ToList();
        var pricesResult = await repository.GetPricesAsync(componentNames);
        
        if (pricesResult.IsFailure)
        {
            return pricesResult.ToProblem();
        }

        var prices = pricesResult.Value;

        var missingComponents = componentNames.Where(c => !prices.ContainsKey(c)).ToList();
        if (missingComponents.Count != 0)
        {
            var detail = $"Missing prices for components: {string.Join(", ", missingComponents)}";
            return Results.BadRequest(new { message = "Missing component prices", detail });
        }

        var currencies = prices.Values.Select(p => p.Currency).Distinct().ToList();
        if (currencies.Count > 1)
        {
            return Results.BadRequest(new
            {
                message = "Currency mismatch",
                detail = $"All components must use the same currency. Found: {string.Join(", ", currencies)}"
            });
        }

        var currency = currencies[0];
        decimal totalCost = 0;
        var breakdown = new List<CostBreakdownItem>();

        foreach (var item in request.Formulation)
        {
            var (unitPrice, _) = prices[item.Component];
            var contribution = (decimal)item.Percentage * unitPrice / 100m; // Assuming percentage is 0-100

            totalCost += contribution;

            breakdown.Add(new CostBreakdownItem(
                item.Component,
                item.Percentage,
                unitPrice,
                contribution));
        }

        MarginAnalysis? marginAnalysis = null;
        if (request.DesiredMarginPercent.HasValue)
        {
            var margin = request.DesiredMarginPercent.Value;
            var marginFactor = margin / 100m;
            
            if (marginFactor >= 1)
            {
                return Results.BadRequest(new { message = "Margin must be less than 100%" });
            }

            var sellingPrice = totalCost / (1 - marginFactor);
            var grossProfit = sellingPrice - totalCost;

            marginAnalysis = new MarginAnalysis(
                request.DesiredMarginPercent.Value,
                Math.Round(sellingPrice, 2),
                Math.Round(grossProfit, 2));
        }

        var response = new CostCalculationResponse(
            request.MaterialId,
            Math.Round(totalCost, 2),
            currency,
            breakdown,
            marginAnalysis);

        return Results.Ok(response);
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

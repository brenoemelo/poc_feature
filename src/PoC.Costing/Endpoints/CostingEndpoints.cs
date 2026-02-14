using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PoC.Costing.Repositories;
using PoC.Shared.Models;

namespace PoC.Costing.Endpoints;

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
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return Results.BadRequest(new { message = "Validation failed", errors = string.Join("; ", errors) });
        }

        logger.LogInformation("Upserting price for component: {ComponentName}", request.ComponentName);
        await repository.UpsertPriceAsync(request);

        return Results.Ok(new { message = "Price updated successfully" });
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
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return Results.BadRequest(new { message = "Validation failed", errors = string.Join("; ", errors) });
        }

        logger.LogInformation("Calculating cost for material: {MaterialId}", request.MaterialId);

        var componentNames = request.Formulation.Select(f => f.Component).Distinct().ToList();
        var prices = await repository.GetPricesAsync(componentNames);

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
                 return Results.BadRequest(new { message = "Invalid margin", detail = "Margin must be less than 100%" });
            }

            var sellingPrice = totalCost / (1 - marginFactor);
            var profit = sellingPrice - totalCost;

            marginAnalysis = new MarginAnalysis(
                margin,
                sellingPrice,
                profit);
        }

        var response = new CostCalculationResponse(
            request.MaterialId,
            totalCost,
            currency,
            breakdown,
            marginAnalysis);

        return Results.Ok(response);
    }
}

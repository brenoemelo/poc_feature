using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenFeature;
using OpenFeature.Model;

namespace PoC.Shared.Infrastructure.Filters;

/// <summary>
/// Minimal API endpoint filter that gates access based on a feature flag.
/// Returns 404 Not Found when the flag is disabled, effectively hiding the feature.
/// </summary>
public sealed class FeatureGateFilter : IEndpointFilter
{
    private readonly string _flagKey;
    private readonly EvaluationContext _evaluationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureGateFilter"/> class.
    /// </summary>
    /// <param name="flagKey">The feature flag key to evaluate.</param>
    public FeatureGateFilter(string flagKey)
    {
        _flagKey = flagKey;
        _evaluationContext = EvaluationContext.Builder()
            .SetTargetingKey("anonymous")
            .Build();
    }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<FeatureGateFilter>>();
        var client = context.HttpContext.RequestServices.GetRequiredService<FeatureClient>();

        try
        {
            var isEnabled = await client.GetBooleanValueAsync(_flagKey, false, _evaluationContext);

            if (!isEnabled)
            {
                logger.LogInformation("Feature '{FlagKey}' is disabled. Returning 404.", _flagKey);
                return Results.NotFound();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to evaluate feature flag '{FlagKey}'. Defaulting to disabled.", _flagKey);
            return Results.NotFound();
        }

        return await next(context);
    }
}

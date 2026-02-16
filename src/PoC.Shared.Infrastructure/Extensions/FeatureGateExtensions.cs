using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PoC.Shared.Infrastructure.Filters;

namespace PoC.Shared.Infrastructure.Extensions;

/// <summary>
/// Extension methods for applying feature flag gates to Minimal API endpoints.
/// </summary>
public static class FeatureGateExtensions
{
    /// <summary>
    /// Gates this endpoint behind a feature flag. Returns 404 if the flag is disabled.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="flagKey">The feature flag key to evaluate.</param>
    /// <returns>The builder for chaining.</returns>
    public static RouteHandlerBuilder WithFeatureGate(this RouteHandlerBuilder builder, string flagKey)
    {
        return builder.AddEndpointFilter(new FeatureGateFilter(flagKey));
    }
}

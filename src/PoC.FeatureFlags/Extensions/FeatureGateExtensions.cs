using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenFeature;

namespace PoC.FeatureFlags.Extensions;

public static class FeatureGateExtensions
{
    public static RouteHandlerBuilder WithFeatureGate(this RouteHandlerBuilder builder, string flagKey)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("FeatureGate");
            var unleash = context.HttpContext.RequestServices.GetRequiredService<Unleash.IUnleash>();

            // Evaluate the feature flag via Unleash directly
            var isEnabled = unleash.IsEnabled(flagKey);

            if (!isEnabled)
            {
                logger.LogWarning("Feature {FlagKey} is disabled. Blocking request.", flagKey);
                return Results.NotFound(); // Or 403 Forbidden
            }

            return await next(context);
        });
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenFeature;

namespace PoC.FeatureFlags.Extensions;

public static partial class FeatureGateExtensions
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Feature {FlagKey} is disabled. Blocking request.")]
    private static partial void LogFeatureDisabled(ILogger logger, string flagKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Checking feature flag: {FlagKey}")]
    private static partial void LogFeatureCheck(ILogger logger, string flagKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Feature {FlagKey} is enabled. Proceeding.")]
    private static partial void LogFeatureEnabled(ILogger logger, string flagKey);

    public static RouteHandlerBuilder WithFeatureGate(this RouteHandlerBuilder builder, string flagKey)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<IFeatureClient>>();
            var featureClient = context.HttpContext.RequestServices.GetRequiredService<IFeatureClient>();

            LogFeatureCheck(logger, flagKey);

            // Evaluate the feature flag via OpenFeature API
            var isEnabled = await featureClient.GetBooleanValueAsync(flagKey, false);

            if (!isEnabled)
            {
                LogFeatureDisabled(logger, flagKey);
                return Results.NotFound(); // Or 403 Forbidden
            }

            LogFeatureEnabled(logger, flagKey);
            return await next(context);
        });
    }
}

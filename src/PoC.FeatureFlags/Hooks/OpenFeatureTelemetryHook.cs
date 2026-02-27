using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenFeature;
using OpenFeature.Model;

namespace PoC.FeatureFlags.Hooks;

public class OpenFeatureTelemetryHook : Hook
{
    private readonly ILogger<OpenFeatureTelemetryHook> _logger;
    private readonly Counter<long> _evaluationRequestsTotal;
    private readonly Counter<long> _evaluationSuccessTotal;
    private readonly Counter<long> _evaluationErrorTotal;

    public OpenFeatureTelemetryHook(ILogger<OpenFeatureTelemetryHook> logger)
    {
        _logger = logger;
        var meter = new Meter("PoC.FeatureFlags", "1.0.0");
        
        _evaluationRequestsTotal = meter.CreateCounter<long>(
            "feature_flag.evaluation_requests_total", 
            "count", 
            "Total number of feature flag evaluation requests");
            
        _evaluationSuccessTotal = meter.CreateCounter<long>(
            "feature_flag.evaluation_success_total", 
            "count", 
            "Total number of successful feature flag evaluations");
            
        _evaluationErrorTotal = meter.CreateCounter<long>(
            "feature_flag.evaluation_error_total", 
            "count", 
            "Total number of failed feature flag evaluations");
    }

    public override ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
    {
        var variant = details.Variant;
        if (string.IsNullOrEmpty(variant))
        {
            variant = details.Value?.ToString() ?? "null";
        }

        var tags = new TagList
        {
            { "key", context.FlagKey },
            { "variant", variant },
            { "reason", details.Reason?.ToString() ?? "UNKNOWN" }
        };

        _evaluationRequestsTotal.Add(1, tags);
        _evaluationSuccessTotal.Add(1, tags);

        _logger.LogInformation(
            "[FeatureFlag] Evaluated '{Key}' to '{Value}' (Variant: '{Variant}', Reason: '{Reason}')", 
            context.FlagKey, 
            details.Value, 
            variant, 
            details.Reason);

        return ValueTask.CompletedTask;
    }

    public override ValueTask ErrorAsync<T>(HookContext<T> context, Exception error, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
    {
        var tags = new TagList
        {
            { "key", context.FlagKey },
            { "reason", "ERROR" }
        };

        _evaluationRequestsTotal.Add(1, tags);
        _evaluationErrorTotal.Add(1, tags);

        _logger.LogError(error, "[FeatureFlag] Error evaluating '{Key}'", context.FlagKey);

        return ValueTask.CompletedTask;
    }
}

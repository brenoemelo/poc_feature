using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Unleash;
using Unleash.Internal;

namespace PoC.Shared.Infrastructure.FeatureFlag;

public class UnleashProvider : FeatureProvider
{
    private readonly IUnleash _unleashClient;

    public UnleashProvider(IUnleash unleashClient)
    {
        _unleashClient = unleashClient;
    }

    public override Metadata GetMetadata()
    {
        return new Metadata("Unleash Provider");
    }

    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var unleashContext = MapContext(context);
            var isEnabled = _unleashClient.IsEnabled(flagKey, unleashContext);
            return Task.FromResult(new ResolutionDetails<bool>(flagKey, isEnabled));
        }
        catch
        {
            return Task.FromResult(new ResolutionDetails<bool>(flagKey, defaultValue, ErrorType.General));
        }
    }

    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var unleashContext = MapContext(context);
            var variant = _unleashClient.GetVariant(flagKey, unleashContext, Variant.DISABLED_VARIANT);
            if (variant != null && variant.IsEnabled)
            {
                return Task.FromResult(new ResolutionDetails<string>(flagKey, variant.Name));
            }

            return Task.FromResult(new ResolutionDetails<string>(flagKey, defaultValue));
        }
        catch
        {
             return Task.FromResult(new ResolutionDetails<string>(flagKey, defaultValue, ErrorType.General));
        }
    }

    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResolutionDetails<int>(flagKey, defaultValue));
    }

    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResolutionDetails<double>(flagKey, defaultValue));
    }

    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResolutionDetails<Value>(flagKey, defaultValue));
    }

    private UnleashContext MapContext(EvaluationContext? context)
    {
        var unleashContext = new UnleashContext();
        
        if (context == null)
        {
            return unleashContext;
        }

        if (!string.IsNullOrEmpty(context.TargetingKey))
        {
            unleashContext.UserId = context.TargetingKey;
        }

        foreach (var key in context.Count > 0 ? context.AsDictionary().Keys : Enumerable.Empty<string>())
        {
            var value = context.GetValue(key);
            if (value.IsString)
            {
                var strValue = value.AsString;
                if (key.Equals("sessionId", StringComparison.OrdinalIgnoreCase))
                {
                    unleashContext.SessionId = strValue;
                }
                else if (key.Equals("remoteAddress", StringComparison.OrdinalIgnoreCase))
                {
                    unleashContext.RemoteAddress = strValue;
                }
                else
                {
                    if (unleashContext.Properties == null)
                    {
                        unleashContext.Properties = new Dictionary<string, string>();
                    }

                    unleashContext.Properties[key] = strValue;
                }
            }
        }

        return unleashContext;
    }
}

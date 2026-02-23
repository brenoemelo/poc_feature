using OpenFeature.Model;
using System.Threading.Tasks;
using Unleash;

namespace PoC.FeatureFlags.Extensions;

public class UnleashFeatureProvider : OpenFeature.FeatureProvider
{
    private readonly IUnleash _unleash;

    public UnleashFeatureProvider(IUnleash unleash)
    {
        _unleash = unleash;
    }

    public override Metadata GetMetadata()
    {
        return new Metadata("Unleash Provider");
    }

    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, System.Threading.CancellationToken cancellationToken = default)
    {
        var isEnabled = _unleash.IsEnabled(flagKey, defaultValue);
        return Task.FromResult(new ResolutionDetails<bool>(flagKey, isEnabled));
    }

    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, System.Threading.CancellationToken cancellationToken = default)
    {
        var variant = _unleash.GetVariant(flagKey);
        var value = variant.Enabled ? (variant.Payload?.Value ?? defaultValue) : defaultValue;
        return Task.FromResult(new ResolutionDetails<string>(flagKey, value));
    }

    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, System.Threading.CancellationToken cancellationToken = default)
    {
        var variant = _unleash.GetVariant(flagKey);
        if (variant.Enabled && int.TryParse(variant.Payload?.Value, out var value))
        {
            return Task.FromResult(new ResolutionDetails<int>(flagKey, value));
        }
        return Task.FromResult(new ResolutionDetails<int>(flagKey, defaultValue));
    }

    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, System.Threading.CancellationToken cancellationToken = default)
    {
        var variant = _unleash.GetVariant(flagKey);
        if (variant.Enabled && double.TryParse(variant.Payload?.Value, out var value))
        {
            return Task.FromResult(new ResolutionDetails<double>(flagKey, value));
        }
        return Task.FromResult(new ResolutionDetails<double>(flagKey, defaultValue));
    }

    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, System.Threading.CancellationToken cancellationToken = default)
    {
        // For PoC simplicity, returning default.
        return Task.FromResult(new ResolutionDetails<Value>(flagKey, defaultValue));
    }
}

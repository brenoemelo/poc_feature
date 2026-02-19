using Unleash;
using Unleash.Internal;
using System.Collections.Generic;
using System.Linq;
using Unleash.Strategies;

namespace PoC.FeatureFlags.Extensions;

public class FakeUnleash : IUnleash
{
    public bool IsEnabled(string toggleName) => true; // Always enable for local dev
    public bool IsEnabled(string toggleName, bool defaultSetting) => true;
    public bool IsEnabled(string toggleName, UnleashContext context) => true;
    public bool IsEnabled(string toggleName, UnleashContext context, bool defaultSetting) => true;
    public Variant GetVariant(string toggleName) => new Variant("enabled", null, true, true);
    public Variant GetVariant(string toggleName, Variant defaultVariant) => new Variant("enabled", null, true, true);
    public Variant GetVariant(string toggleName, UnleashContext context) => new Variant("enabled", null, true, true);
    public Variant GetVariant(string toggleName, UnleashContext context, Variant defaultVariant) => defaultVariant;
    public void Dispose() { }
    public ICollection<ToggleDefinition> ListKnownToggles() => new List<ToggleDefinition>(); // Fix missing member
}

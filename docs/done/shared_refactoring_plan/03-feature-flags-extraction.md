# 03 - Extraction Plan: PoC.FeatureFlags

## New Project Structure
Create a new Class Library: `src/PoC.FeatureFlags/PoC.FeatureFlags.csproj`.

### Dependencies (NuGet)
Move these from `PoC.Shared.Infrastructure` to `PoC.FeatureFlags`:
*   `Unleash.Client`
*   `OpenFeature`
*   `OpenFeature.Contrib.Providers.Unleash` (if used, or custom provider)

### Code Migration
1.  **Move Extensions**:
    *   Move `src/PoC.Shared.Infrastructure/Extensions/ServiceCollectionExtensions.cs` -> `AddPoCFeatureFlags` method.
    *   Extract it into: `PoC.FeatureFlags.Extensions.FeatureFlagsExtensions`.

2.  **Move Feature Flag Manager**:
    *   Move `FeatureFlagManager.cs` (if it exists as a wrapper) to this project.
    *   Ensure `IFeatureFlagManager` interface is also moved (or kept in Core if purely abstract, but likely better here to keep Core clean).

3.  **Namespace Updates**:
    *   Change `PoC.Shared.Infrastructure.FeatureFlags` to `PoC.FeatureFlags`.

### Configuration
Standardize the configuration:

```csharp
public class FeatureFlagOptions
{
    public string UnleashApiUrl { get; set; }
    public string ApiKey { get; set; }
    public string AppName { get; set; }
    public string InstanceId { get; set; }
    public int FetchTogglesIntervalSeconds { get; set; } = 30;
}
```

### Initialization Usage

```csharp
builder.Services.AddPoCFeatureFlags(o => 
{
    o.UnleashApiUrl = builder.Configuration["FeatureFlags:UnleashApiUrl"];
    o.ApiKey = builder.Configuration["FeatureFlags:UnleashApiKey"];
    o.AppName = "PoC-Costing";
});
```

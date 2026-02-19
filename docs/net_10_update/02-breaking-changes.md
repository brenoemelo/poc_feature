# 02 - Breaking Changes & Code Updates

## Potential Breaking Changes
Upgrading from .NET 8 to .NET 10 spans two major versions (.NET 9 STS and .NET 10 LTS).

### 1. ASP.NET Core
*   **Middleware Behavior**: Check for deprecations in `UseRouting`, `UseEndpoints`, or authentication handlers.
*   **Minimal APIs**: .NET 10 may enforce stricter typing or change how `MapGroup` filters are applied.
*   **JSON Serialization**: Default settings for `System.Text.Json` might change (e.g., stricter validation).

### 2. Entity Framework Core (If applicable)
*   Although this project uses basic DynamoDB SDK, if EF Core is introduced, verify query breaking changes.

### 3. AWS SDK
*   The `AWSSDK.Extensions.NETCore.Setup` and `Amazon.Lambda.*` packages must be compatible.
*   *Action*: Ensure we are on version `4.x` (or whatever the latest major is for .NET 10 support) of the AWS Lambda libraries.

## Code Adjustments

### Global.json
Update the SDK version constraint:
```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

### Compiler Warnings
Newer SDKs often introduce new analyzers.
*   **Nullability**: Ensure `Nullable` context is respected.
*   **Code Style**: Regressions in `EnforceCodeStyleInBuild` might trigger new build errors.
*   *Strategy*: Fix warnings as they appear; treat warnings as errors to maintain quality.

### Records & Pattern Matching
Leverage new C# features to simplify DTOs.
*   Refactor verbose classes to `record struct` where appropriate for memory optimization in high-volume Lambda processing.

### Performance Tuning
*   Review usage of `params` collections (C# 13/14 improvements).
*   Check for `Span<T>` opportunities in string parsing logic (Material/Costing ingestion).

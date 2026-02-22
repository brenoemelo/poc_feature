# 04 - Migration Guide for Consumers

This guide explains how to update existing services (`PoC.Costing`, `PoC.Materials`) after the shared libraries have been refactored.

## Step 1: Remove Old Reference
In your service's `.csproj`:

```xml
<!-- REMOVE this if you no longer need the "Kitchen Sink" -->
<!-- <ProjectReference Include="..\PoC.Shared.Infrastructure\PoC.Shared.Infrastructure.csproj" /> -->
```

## Step 2: Add New References
Add the specific capabilities you need:

```xml
<ItemGroup>
    <ProjectReference Include="..\PoC.Observability\PoC.Observability.csproj" />
    <ProjectReference Include="..\PoC.FeatureFlags\PoC.FeatureFlags.csproj" />
    <!-- Keep Core Shared if needed for DTOs/Validators -->
    <ProjectReference Include="..\PoC.Shared\PoC.Shared.csproj" />
</ItemGroup>
```

## Step 3: Update `Program.cs`

### Before
```csharp
using PoC.Shared.Infrastructure.Extensions;

builder.AddPoCObservability("PoC-Costing", "1.0.0");
builder.Services.AddPoCFeatureFlags(builder.Configuration);
```

### After
```csharp
using PoC.Observability.Extensions;
using PoC.FeatureFlags.Extensions;

// Observability
builder.Services.AddPoCObservability(o => {
    o.ServiceName = "PoC-Costing";
    o.OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
});

// Feature Flags
builder.Services.AddPoCFeatureFlags(o => {
    o.UnleashApiUrl = builder.Configuration["FeatureFlags:UnleashApiUrl"];
    // ... maps from key
});
```

## Step 4: Verify Compilation
Run `dotnet build` to catch any namespace errors. You may need to update `using` statements in your classes:

*   `using PoC.Shared.Infrastructure.Observability;` -> `using PoC.Observability;`
*   `using PoC.Shared.Infrastructure.FeatureFlags;` -> `using PoC.FeatureFlags;`

## Step 5: Remove Serilog (Crucial for Native AOT)
1.  **Delete**: `Log.Logger = ...` configuration in `Program.cs`.
2.  **Delete**: `.UseSerilog()` from the host builder.
3.  **Replace**: Any static `Log.Information()` calls with injected `ILogger<T>`.
    *   *Tip*: Use `[LoggerMessage]` source generators for high-performance logging.

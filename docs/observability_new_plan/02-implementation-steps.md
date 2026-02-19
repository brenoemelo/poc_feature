# 02 - Implementation Steps

## Step 1: Add NuGet Dependencies
The core missing piece is `OpenTelemetry.Instrumentation.AWSLambda`.

**Action**: Run the following command in `src/PoC.Shared.Infrastructure`:

```powershell
dotnet add src/PoC.Shared.Infrastructure/PoC.Shared.Infrastructure.csproj package OpenTelemetry.Instrumentation.AWSLambda
```

## Step 2: Configure Service Collection
We need to register the Lambda instrumentation in the DI container. This ensures that when the app starts, it hooks into the AWS X-Ray context reader.

**File**: `src/PoC.Shared.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

**Changes**:
Locate the `ConfigureOpenTelemetry` method and modify the tracing configuration:

```csharp
// Inside services.AddOpenTelemetry().WithTracing(tracing => { ... })

tracing
    .SetResourceBuilder(resourceBuilder)
    .AddSource(serviceName)
    .AddHttpClientInstrumentation(o => o.FilterHttpRequestMessage = httpFilter)
    .AddAWSInstrumentation() // Existing
    // NEW: Add Lambda Instrumentation
    .AddAWSLambdaConfigurations(options => 
    {
        // Vital: Disable X-Ray context extraction here because 
        // explicit X-Ray SDK is not used, but we want OTel to handle it.
        // NOTE: In some versions, this might be named differently or require 
        // different bool logic depending on if you use the X-Ray propagator.
        // For pure OTel, we usually enable the propagator elsewhere.
        options.DisableAwsXRayContextExtraction = true;
    });

if (isWeb)
{
    // ... existing
}
```

## Step 3: Lifecycle Management (Flushing)
In a standard Lambda function (raw handler), we would wrap the handler. Since we are using **ASP.NET Core Hosting**, the `Amazon.Lambda.AspNetCoreServer.Hosting` library manages the lifecycle.

However, OTel needs to flush *before* the environment freezes.
The `OpenTelemetry.Instrumentation.AWSLambda` package automatically registers a lifecycle hook if `AddAWSLambdaConfigurations` is called.

**Verification**:
Ensure that `FlushTimeout` is configured reasonably.

```csharp
.AddAWSLambdaConfigurations(options =>
{
    options.DisableAwsXRayContextExtraction = true;
});
```

*Note: For the ASP.NET Core adapter, ensure you are using a version of `Amazon.Lambda.AspNetCoreServer.Hosting` that supports `ILambdaContext` injection if you need manual access, but for basic tracing, the standard setup above is sufficient.*

## Step 4: Verify Environment Variables
Ensure your deployment scripts (`deployment/localstack/utils.ps1`) inject the OTLP endpoint.

**Required Variables**:
```powershell
OTEL_EXPORTER_OTLP_ENDPOINT="http://otel-collector:4318"
OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf"
```
*Current `utils.ps1` already has this, so no action needed there.*

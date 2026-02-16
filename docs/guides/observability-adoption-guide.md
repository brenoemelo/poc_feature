# Observability Adoption Guide

## 🎯 Objective
This guide explains how to **migrate another .NET 8 microservices project** to use the standardized OpenTelemetry (OTLP) observability solution developed in this PoC.

By adopting this standard, your project gains:
- **Vendor Agnosticism:** Switch between Datadog, Grafana, Jaeger, etc. just by changing config.
- **Unified Logging:** Logs are structured and correlated with Traces automatically.
- **Auto-Instrumentation:** AWS SDK, HTTP Client, and ASP.NET Core instrumentation out-of-the-box.

---

## 🛠️ Step 1: The Shared Library

The core logic resides in `PoC.Shared.Infrastructure`. You have two options:

### Option A: NuGet Package (Recommended)
Publish `PoC.Shared.Infrastructure` as a NuGet package to your private feed (Azure Artifacts / NuGet.org) and install it.

### Option B: Copy & Paste (Quick Start)
If you cannot publish packages yet, verify you have these dependencies in your project's `.csproj`:

```xml
<ItemGroup>
  <!-- Serilog -->
  <PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
  <PackageReference Include="Serilog.Enrichers.OpenTelemetry" Version="1.0.1" />
  
  <!-- OpenTelemetry -->
  <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.11.1" />
  <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.11.0" />
  <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.11.0" />
  <PackageReference Include="OpenTelemetry.Instrumentation.AWS" Version="1.11.0" />
  <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.11.1" />
</ItemGroup>
```

Then, migrate the following files to your project's `Shared` or `Infrastructure` layer:
1. `src/PoC.Shared.Infrastructure/Configuration/OtelOptions.cs`
2. `src/PoC.Shared.Infrastructure/Extensions/ServiceCollectionExtensions.cs` (The `AddPoCObservability` method)
3. `src/PoC.Shared.Infrastructure/Extensions/WebApplicationExtensions.cs` (The `UsePoCDefaults` middleware)
4. `src/PoC.Shared.Infrastructure/Extensions/TraceIdResponseMiddleware.cs`

---

## 💻 Step 2: Implementation

### 1. Program.cs
Modify your `Program.cs` to initialize observability **before** `builder.Build()` and middleware **after**.

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. ADD THIS LINE
// "MyService" -> Names the service in the backend (e.g. Datadog Service Param)
builder.AddPoCObservability("My-New-Service", "1.0.0");

// ... other services ...

var app = builder.Build();

// 2. ADD THIS LINE (Must be early in the pipeline)
app.UsePoCDefaults();

// ... map endpoints ...

app.Run();
```

### 2. appsettings.json
Add the `Otel` configuration section to `appsettings.json`.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Otel": {
    "Endpoint": null, // Default: Console only (safe for local dev)
    "Protocol": "grpc",
    "Environment": "local"
  }
}
```

---

## ☁️ Step 3: Infrastructure & Configuration (IaC)

To send data to a backend (Datadog, Grafana Cloud, Jaeger), you **do not change code**. You set Environment Variables in your deployment (Terraform/CloudFormation/Helm).

### Required Variables

| Variable | Description | Example (Datadog) | Example (Grafana) |
|---|---|---|---|
| `OTEL__ENDPOINT` | Receiver URL | `http://datadog-agent:4317` | `https://otlp-gateway.grafana.net/otlp` |
| `OTEL__PROTOCOL` | Transport | `grpc` | `http` |
| `OTEL__HEADERS` | Auth Headers | (None for Agent) | `Authorization=Basic <base64>` |
| `OTEL__ENVIRONMENT`| Env tag | `staging` | `production` |

### Terraform Example (AWS Lambda)

If you are using Terraform to deploy your Lambda/Container:

```hcl
resource "aws_lambda_function" "my_service" {
  function_name = "my-service"
  # ...
  
  environment {
    variables = {
      # Observability Config
      OTEL__ENDPOINT    = var.otel_endpoint
      OTEL__PROTOCOL    = "http" # Lambda usually works best with HTTP
      OTEL__ENVIRONMENT = var.environment
    }
  }
}
```

---

## 🧪 Step 4: Verification

1. **Run Locally:** Start the app. Logs should appear in Console (JSON format) with `TraceId`.
2. **Connect Local Stack:** 
   - Point `OTEL__ENDPOINT` to `http://localhost:4317`
   - Use the [Local Observability Stack](./observability.md#local-observability-stack-docker) to see Traces in Grafana.

---

## ✅ Migration Checklist

- [ ] **Dependencies:** Installed `Serilog` and `OpenTelemetry` packages.
- [ ] **Shared Code:** Copied `Extensions` and `Configuration` (or installed NuGet).
- [ ] **Startup:** Called `AddPoCObservability` and `UsePoCDefaults`.
- [ ] **Config:** Added `Otel` section to `appsettings.json`.
- [ ] **IaC:** Updated deployment scripts to ingest `OTEL__*` env vars.
- [ ] **Cleanup:** Removed old logging/tracing code (e.g., direct Serilog config, X-Ray SDK).

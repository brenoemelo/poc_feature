# Observability Design Specification

**Role:** Senior Observability Architect
**Goal:** Implement a fully vendor-agnostic observability stack (Logging, Tracing, Metrics) using the OpenTelemetry Protocol (OTLP).

## Core Philosophy
The system relies exclusively on standardized protocols. No vendor-specific SDKs (Datadog, Grafana, etc.) are permitted in the codebase. All routing and authentication are handled via configuration.

---

## 1. Technical Components

### Shared Extensions (`ServiceCollectionExtensions.cs`)
Provides a single entry point for all microservices:
```csharp
builder.AddPoCObservability("Service-Name", "1.0.0");
```

**Key Features:**
- **Serilog Integration:** Configures structured logging with JSON output.
- **Log Correlation:** Injects `TraceId` and `SpanId` into every log message to enable "Logs to Traces" navigation in backends.
- **Resource Detection:** Mapped attributes for vendor compatibility:
  - `service.name`
  - `service.version`
  - `service.instance.id` (Unique per cold start)
  - `deployment.environment` (Mapped to `env` in Datadog)
- **Startup Metrics:** Automatically tracks and emits `app.startup_duration_ms`.
- **Flexible Exporter:** Supports both gRPC and HTTP/Protobuf protocols with custom headers.

---

## 2. Configuration Structure

The `Otel` section in `appsettings.json` controls the behavior across environments.

### appsettings.json (Shared Defaults)
```json
{
  "Otel": {
    "Endpoint": null,
    "Protocol": "grpc",
    "Headers": null,
    "Environment": "development"
  }
}
```

### Multi-Backend Scenarios

#### Scenario A: Grafana Cloud (Direct Ingest)
Requires HTTP/Protobuf and Basic Auth headers.
```json
{
  "Otel": {
    "Endpoint": "https://otlp-gateway.grafana.net/otlp",
    "Protocol": "http",
    "Headers": "Authorization=Basic <base64_token>",
    "Environment": "production"
  }
}
```

#### Scenario B: Datadog Agent (Localhost)
Standard gRPC connection to a local agent.
```json
{
  "Otel": {
    "Endpoint": "http://localhost:4317",
    "Protocol": "grpc",
    "Environment": "staging"
  }
}
```

---

## 3. Environment Variable Toggles

For CI/CD and production overrides, use these environment variables:

| Variable | Description | Example |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Active environment file | `Production` |
| `OTEL__ENDPOINT` | Backend URL | `https://otlp-gateway.net` |
| `OTEL__PROTOCOL` | `grpc` or `http` | `http` |
| `OTEL__HEADERS` | Authentication tokens | `Authorization=Basic xxx` |
| `OTEL__ENVIRONMENT` | Semantic attribute | `production` |

---

## 4. One-Liner Usage Example

### Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);

// Configures Serilog + OTel (Tracing, Metrics, Logs)
builder.AddPoCObservability("PoC-Materials", "1.0.0");

var app = builder.Build();

// Configures shared exception handling + request logging
app.UsePoCDefaults();

app.Run();
```

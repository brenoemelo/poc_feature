# Observability Guide

The PoC project implements a **vendor-agnostic observability stack** using the OpenTelemetry Protocol (OTLP). This guide explains how to monitor, debug, and trace the system.

---

## 🏗️ Architecture
The observability stack is centrally managed in the `PoC.Shared.Infrastructure` library. Every microservice automatically inherits best-in-class instrumentation.

- **Logging:** Structured JSON logs via Serilog.
- **Tracing:** Distributed tracing with automatic context propagation.
- **Metrics:** Performance tracking (e.g., startup duration).
- **Correlation:** Every log entry is enriched with `TraceId` and `SpanId`.

---

## 🛠️ Configuration

Monitoring backends are configured solely through `appsettings.json` or Environment Variables. No vendor SDK is required.

### 1. Active Backend (OTLP)
Change the `Endpoint` to point to your collector or vendor gateway:

| Scenario | Mode | Endpoint | Protocol |
|---|---|---|---|
| Local Dev | Console | `null` | N/A |
| Jaeger | gRPC | `http://localhost:4317` | `grpc` |
| Datadog Agent | gRPC | `http://datadog-agent:4317` | `grpc` |
| Grafana Cloud | HTTP/Pbuf | `https://otlp-gateway.grafana.net/otlp` | `http` |

### 2. Environment Overrides
In production, sensitive headers (like Auth tokens) should be injected via environment variables:

```bash
OTEL__ENDPOINT=https://otlp-gateway.grafana.net/otlp
OTEL__PROTOCOL=http
OTEL__HEADERS=Authorization=Basic xxx
OTEL__ENVIRONMENT=production
```

---

## 📝 Developer Workflow

### One-Liner Integration
To add observability to a new service, just add this to `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Standard setup: Logging + Traces + Metrics
builder.AddPoCObservability("PoC-NewService", "1.0.0");

var app = builder.Build();

// Standard middleware: Exception Handling + Request Logging
app.UsePoCDefaults();
```

### Writing Logs
Always use structured logging:

```csharp
// ❌ Avoid string interpolation
Log.Information($"Created material {material.Id}");

// ✅ Use message templates (better for indexing)
Log.Information("Created material {MaterialId}", material.Id);
```

---

## 🔍 Verification

### 1. Local Verification (Console)
By default, logs and spans are printed to the console in JSON format.
- Look for `TraceId` and `SpanId` fields in the output.
- Check for `app.startup_duration_ms` metric on startup.

### 2. AWS Lambda Status
Check current Lambda logs in LocalStack:
```bash
awslocal logs tail /aws/lambda/PoC-Materials-MaterialsFunction-prod --follow
```

---

## 💡 Troubleshooting

### "Missing Logs to Traces linkage"
Ensure you are using `builder.AddPoCObservability()`. This method configures the Serilog enricher that injects OTel context into the logs.

### "No metrics reaching the backend"
- Verify your `OTEL__ENDPOINT` is reachable from the network.
- Ensure the `OTEL__PROTOCOL` matches what the backend expects (`grpc` vs `http`).
- Check if your backend requires custom `OTEL__HEADERS` (e.g., `X-Scope-OrgId` or `Authorization`).

---

## 🔗 Related Resources
- [ADR 003: Shared Observability Library](../adr/003-shared-observability-library.md)
- [Technical Design](../observability-design.md)

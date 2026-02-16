# Observability Guide

The PoC project implements a **vendor-agnostic observability stack** using the OpenTelemetry Protocol (OTLP). This guide explains how to monitor, debug, and trace the system using the provided **Local Observability Stack**.

---

## 🏗️ Architecture
The observability stack is centrally managed in the `PoC.Shared.Infrastructure` library. Every microservice automatically inherits best-in-class instrumentation.

- **Logging:** Structured JSON logs via Serilog.
- **Tracing:** Distributed tracing with automatic context propagation.
- **Metrics:** Performance tracking (e.g., startup duration).
- **Correlation:** Every log entry is enriched with `TraceId` and `SpanId`.

---

## 🚀 Local Observability Stack (Docker)

We provide a complete pre-configured stack running in Docker.

### 1. Requirements
- Docker Desktop / Docker Compose
- `poc-net` network must exist (usually created by the main `docker-compose.yaml`):
  ```bash
  docker network create poc-net || true
  ```

### 2. Start the Stack
Run the following command from the repository root:

```bash
docker compose -f docker/observability/docker-compose.yaml up -d
```

This will start:
- **OTel Collector** (`:4317` gRPC / `:4318` HTTP / `:8889` Prom)
- **Tempo** (Traces)
- **Prometheus** (Metrics)
- **Loki** (Logs)
- **Grafana** (`http://localhost:3000`)

### 3. Connect .NET Services
To send telemetry to this local stack, configure your application (or IDE launch profile) with:

**Option A: appsettings.json**
```json
{
  "Otel": {
    "Endpoint": "http://localhost:4317",
    "Protocol": "grpc",
    "Environment": "local-docker"
  }
}
```

**Option B: Environment Variables**
```bash
OTEL__ENDPOINT=http://localhost:4317
OTEL__PROTOCOL=grpc
```

---

## 📊 Using Grafana

1. Open [http://localhost:3000](http://localhost:3000).
2. Go to **Explore** (Compass icon).
3. Select a Datasource:
   - **Tempo:** Search for traces by ID or filter by Service Name.
   - **Prometheus:** Query metrics (e.g., `http_server_request_duration_seconds_bucket`).
   - **Loki:** Query logs (e.g., `{service_name="PoC-Materials"}`).

> **Pro Tip:** Logs in Loki contain a "TraceID" link that jumps directly to the Trace in Tempo.

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

### 1. Check Integration
Run the stack and your app. look at the OTel Collector logs:

```bash
docker compose -f docker/observability/docker-compose.yaml logs -f otel-collector
```

You should see "TracesExporter", "MetricsExporter", "LogsExporter" outputting data if the `debug` exporter is enabled.

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

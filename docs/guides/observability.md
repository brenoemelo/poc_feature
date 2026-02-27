# Observability Guide

The PoC project uses a **Vendor-Agnostic** observability stack based on the **OpenTelemetry (OTel)** standard. This ensures we can switch backends (e.g., from Tempo to Datadog) without changing application code.

## 1. The Implementation
All observability logic is centralized in the **`PoC.Observability`** library. 

- **Rule:** Do not add OpenTelemetry packages directly to microservices.
- **Rule:** Use `Microsoft.Extensions.Logging` (native `ILogger`). **Serilog has been removed.**
- **Registration:** Simply call `builder.AddPoCObservability("ServiceName", "1.0.0")` in your `Program.cs`.

## 2. The Stack (Local)

We use a pre-configured Docker stack (part of the root `docker-compose.yml`):

| Component | Role | Port |
|---|---|---|
| **OTel Collector** | The "Router". Receives telemetry from apps and forwards it. | `:4317` (gRPC), `:4318` (HTTP) |
| **Tempo** | **Distributed Tracing** backend (stores traces). | Internal |
| **Prometheus** | **Metrics** backend (stores time-series data). | `:9090` |
| **Loki** | **Logging** backend (stores logs). | `:3100` |
| **Grafana** | **Visualization** UI. | [`http://localhost:3000`](http://localhost:3000) |

## 3. How to: Find a Trace

Tracing is the most powerful tool for debugging distributed transactions (e.g., API -> SNS -> Worker).

### Scenario: "My request failed, here is the Trace ID"
When an API request fails, the API returns a `traceId` field in the `ProblemDetails` JSON.

1. Copy the `traceId` (e.g., `5b8aa5a2d2c872e9...`).
2. Open **Grafana** -> **Explore**.
3. Select **Tempo** as the datasource.
4. Paste the ID into the "Trace ID" field and run query.
5. **Result:** You will see a Gantt chart showing the API Latency, DynamoDB calls, and SNS Publishing.

### Scenario: "I want to see traces for a specific error"
1. Select **Loki** datasource.
2. Query logs with error level: `{job="PoC.Materials"} |= "error"`.
3. Expand a log line.
4. Click the **"Tempo"** button next to the `TraceID` field.

## 4. Metrics & Performance

We track key metrics to ensure system health.

- **`http.server.request.duration`**: Latency of API requests.
- **`business.costing.value`**: Custom metric tracking calculated costs.
- **`process.runtime.dotnet.*`**: .NET Runtime metrics (GC, CPU, ThreadPool).
- **`service_graph_request_total`**: Service dependency metrics (APM Map).

## 5. Troubleshooting Missing Telemetry

**Symptoms:** No traces in Grafana.

**Checklist:**
1. **Is the OTel Collector running?**
   ```bash
   docker ps | grep otel-collector
   ```
2. **Is the service using `PoC.Observability`?**
   - Check `Program.cs` for `AddPoCObservability`.
3. **Is the App pointing to the Collector?**
   - Check environment variables: `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317`.
4. **Are logs showing errors?**
   - Look for "Connection refused" or "Export failed" in the app console output.

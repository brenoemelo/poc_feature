# Observability Guide

The PoC project uses a **Vendor-Agnostic** observability stack based on the **OpenTelemetry (OTel)** standard. This ensures we can switch backends (e.g., from Tempo to Datadog) without changing application code.

## 1. The Stack (Local)

We use a pre-configured Docker stack (`docker/observability/docker-compose.yaml`):

| Component | Role | Port |
|---|---|---|
| **OTel Collector** | The "Router". Receives telemetry from apps and forwards it. | `:4317` (gRPC), `:4318` (HTTP) |
| **Tempo** | **Distributed Tracing** backend (stores traces). | Internal |
| **Prometheus** | **Metrics** backend (stores time-series data). | `:9090` |
| **Loki** | **Logging** backend (stores logs). | `:3100` |
| **Grafana** | **Visualization** UI. | [`http://localhost:3000`](http://localhost:3000) |

## 2. How to: Find a Trace

Tracing is the most powerful tool for debugging distributed transactions (e.g., API -> SNS -> Worker).

### Scenario: "My request failed, here is the Trace ID"
When an API request fails, the API returns a `traceId` field in the ProblemDetails JSON.

1. Copy the `traceId` (e.g., `5b8aa5a2d2c872e9...`).
2. Open **Grafana** -> **Explore**.
3. Select **Tempo** as the datasource.
4. Paste the ID into the "Trace ID" field and run query.
5. **Result:** You will see a Gantt chart showing:
   - The API Latency.
   - DynamoDB calls.
   - SNS Publishing.
   - (If configured) The async worker processing the message.

### Scenario: "I want to see traces for a specific error"
1. Select **Loki** datasource.
2. Query logs with error level: `{app="PoC.Materials"} |= "error"`.
3. Expand a log line.
4. Click the **"Tempo"** button next to the `TraceID` field.

## 3. Metrics & Performance

We track key metrics to ensure system health.

### Key Metrics
- **`app.startup_duration_ms`**: Measures the time from process start to "Ready".
  - **Goal:** < 500ms for Cold Starts.
  - **Usage:** Used to detect slow initialization logic (e.g., heavy reflection or DB connection setup).
- **`http.server.request.duration`**: Latency of API requests.
- **`process.runtime.dotnet.gc.collections.count`**: Garbage Collection frequency.
  - **High Gen2 count?** Indicates memory leaks or inefficient object allocation.

## 4. Troubleshooting Missing Telemetry

**Symptoms:** No traces in Grafana.

**Checklist:**
1. **Is the OTel Collector running?**
   ```bash
   docker ps | grep otel-collector
   ```
2. **Is the App pointing to the Collector?**
   - Check `appsettings.json`: `"Otel": { "Endpoint": "http://localhost:4317" }`.
   - **Docker:** Must use `http://otel-collector:4317`.
   - **Localhost:** Must use `http://localhost:4317`.
3. **Are logs showing errors?**
   - Look for "Connection refused" or "Export failed" in the app console.

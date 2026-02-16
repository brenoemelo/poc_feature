# Datadog Migration Guide

## 🎯 Objective
This guide explains how to configure the existing OpenTelemetry (OTLP) instrumentation to send Traces, Metrics, and Logs to **Datadog**.

Our application uses the **OpenTelemetry Protocol (OTLP)**. Since Datadog supports OTLP natively (via the Datadog Agent or direct ingestion), **no code changes are required**. Migration is purely a configuration change.

---

## 🏗️ Architecture Options

You can send telemetry to Datadog in two ways:

1.  **Via Datadog Agent (Recommended for K8s/EC2)**: App -> OTLP (gRPC) -> Datadog Agent -> Datadog Backend.
2.  **Direct OTLP Ingestion (Serverless/Lambda)**: App -> OTLP (HTTP/Protobuf) -> Datadog API.

---

## 🛠️ Configuration Guide

### Option 1: Using Datadog Agent (Standard)
*Best for: Kubernetes, Docker, VM deployments.*

**Prerequisite:** Ensure the Datadog Agent is running and OTLP gRPC is enabled (usually ports `4317`).

**Development (`appsettings.json`):**
```json
{
  "Otel": {
    "Endpoint": "http://datadog-agent:4317",
    "Protocol": "grpc",
    "ResourceAttributes": {
      "deployment.environment": "staging"
    }
  }
}
```

**Environment Variables (ECS/K8s):**
```bash
OTEL__ENDPOINT=http://localhost:4317  # or agent-host:4317
OTEL__PROTOCOL=grpc
OTEL__RESOURCEATTRIBUTES=deployment.environment=staging
```

### Option 2: Direct OTLP Ingestion
*Best for: AWS Lambda, Azure Functions, or environments where Agent installation is difficult.*

**Prerequisite:** You need your Datadog API Key.

**Development (`appsettings.json`):**
```json
{
  "Otel": {
    "Endpoint": "https://otlp.datadoghq.com/v1/traces", 
    "Protocol": "http",
    "Headers": "DD-API-KEY=<YOUR_API_KEY>",
    "ResourceAttributes": {
      "deployment.environment": "production"
    }
  }
}
```
*Note: For metrics, the endpoint is specific. It is often simpler to point to a local OTel Collector that forwards to Datadog.*

**Environment Variables (Lambda):**
```bash
OTEL__ENDPOINT=https://otlp.datadoghq.com/v1/traces
OTEL__PROTOCOL=http
OTEL__HEADERS=DD-API-KEY=your_actual_api_key
OTEL__RESOURCEATTRIBUTES=deployment.environment=production
```

---

## 🔍 Semantic Mapping

Our shared library automatically maps OpenTelemetry attributes to Datadog tags:

| OpenTelemetry Attribute | Datadog Tag | Source |
|---|---|---|
| `service.name` | `service` | Changed via `AddPoCObservability("MyService")` |
| `service.version` | `version` | Changed via `AddPoCObservability("...", "1.0.0")` |
| `deployment.environment` | `env` | Set via `appsettings` or generic ENV var |

---

## 📋 Execution Plan

Tasks necessary to complete the migration to Datadog:

### Phase 1: Infrastructure Preparation
- [ ] **Provision Datadog Account:** Ensure access to API Keys and Dashboard.
- [ ] **Deploy Datadog Agent:** (If using Option 1) Install Agent in the target environment (K8s DaemonSet or Sidecar) with `otlp_config.receiver.protocols.grpc` enabled.

### Phase 2: Application Configuration
- [ ] **Configure ECS/K8s Env Vars:** Update task definitions to set `OTEL__ENDPOINT` to the agent URL.
- [ ] **Set Environment Tag:** Ensure `OTEL__RESOURCEATTRIBUTES=deployment.environment=production` is set correctly for filtering.

### Phase 3: Verification
- [ ] **Verify Connection:** Restart services and check Agent status (`agent status`).
- [ ] **Check Datadog APM:** Verify services appear in "APM > Services".
- [ ] **Validate Correlation:** Open a Trace and verify "Host" and "Logs" tabs show correlated data.
- [ ] **Dashboard Creation:** Create a standard dashboard for standard .NET metrics (GC, Requests/sec, Latency).

### Phase 4: Cleanup
- [ ] **Disable Legacy Exporters:** If any legacy monitoring was used, remove it to save costs.

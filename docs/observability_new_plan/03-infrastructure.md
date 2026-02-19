# 03 - Infrastructure (Docker & Collector)

## Current Setup vs. Guide
The current `docker-compose.yml` uses **individual containers** for the LGTM stack (Loki, Grafana, Tempo, Mimir/Prometheus). This is a valid, granular approach that offers more control than the "All-in-One" image mentioned in the guide.

**Status**: We will **KEEP** the current Docker Compose setup. It is already working and correctly configured.

## OpenTelemetry Collector Configuration
The collector acts as the gateway. It must be configured to receive OTLP from the Lambda (which runs inside the Docker network) and export to the backend services.

**File**: `docker/observability/otel-collector-config.yaml`

### Key Configuration Verification
Ensure the `receivers` and `exporters` match the networking of your Docker Compose.

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

exporters:
  otlp/tempo:
    endpoint: tempo:4317 # Points to the 'tempo' service in docker-compose
    tls: { insecure: true }

  prometheus:
    endpoint: "0.0.0.0:8889" # Prometheus scrapes this

  loki:
    endpoint: http://loki:3100/loki/api/v1/push # Points to 'loki' service
```

*Note: The current configuration in the project uses `otlp/tempo`, `prometheus`, and `loki` exporters. This aligns perfectly with the architecture.*

## Networking
For the Lambda (running in LocalStack) to reach the Collector:
1.  **Network**: Both must be on the same Docker network (e.g., `poc-net`).
2.  **Hostname**: The Lambda environment uses the container name `otel-collector` to resolve the address.
    *   This is why `OTEL_EXPORTER_OTLP_ENDPOINT="http://otel-collector:4318"` is crucial in `utils.ps1`.

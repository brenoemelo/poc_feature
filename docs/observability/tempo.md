# Tempo & Distributed Tracing Guide

This document explains how to use **Grafana Tempo** for distributed tracing in the PoC project.

## 1. Overview
Tempo is a high-volume, minimal dependency distributed tracing backend. It is integrated with:
- **OpenTelemetry Collector**: Receives traces from services (via OTLP).
- **Grafana**: Visualizes traces and links them to logs and metrics.
- **Loki**: Correlates logs with traces via `TraceId`.

## 2. Architecture Flow
1. **Application**: Generates spans (activities) and exports them to OTel Collector (gRPC 4317).
2. **OTel Collector**:
   - Batches spans.
   - Generates RED metrics (Rate, Errors, Duration) via `spanmetrics` connector.
   - Exports traces to **Tempo** (gRPC 4317/4319).
3. **Tempo**: Stores traces.
4. **Grafana**: Queries Tempo via **TraceQL**.

## 3. Accessing Traces in Grafana
1. Open Grafana: http://localhost:3000
2. Go to **Explore** (Compass icon).
3. Select **Tempo** datasource.

### 3.1. Query Types
Tempo supports two query types:
- **Search**: Find traces by tags (service name, status, duration).
- **TraceQL**: Powerful query language (similar to PromQL/LogQL).

## 4. Common TraceQL Queries

### Find Traces by Service
```traceql
{ span.service.name = "PoC-Materials-Ingestion" }
```

### Find Errors
```traceql
{ status = "error" }
```

### Find Slow Requests (> 1s)
```traceql
{ duration > 1s }
```

### Complex Filtering (Service + Error)
```traceql
{ span.service.name = "PoC-Materials-Ingestion" && status = "error" }
```

### Filter by Attribute (e.g., HTTP Method)
```traceql
{ span.http.method = "POST" }
```

## 5. Troubleshooting Missing Traces

### 5.1. Check OTel Collector
Ensure the Collector is receiving spans. Check logs (if verbose):
```bash
docker logs otel-collector
```
*Note: Verbosity is set to `normal` by default to avoid log spam.*

### 5.2. Check Application Configuration
Ensure environment variables are set correctly in `infra-config.env` or Lambda config:
- `OTEL_EXPORTER_OTLP_ENDPOINT`: `http://otel-collector-1:4317`
- `OTEL_SERVICE_NAME`: (Must be unique per service)

### 5.3. Trace Propagation
Ensure `TraceId` is propagated:
- HTTP headers: `traceparent` (W3C standard).
- AWS SNS/SQS: Attributes `TraceId` / `SpanId`.

## 6. Integration with Logs (Loki)
Traces are linked to logs via `TraceID`.
1. Find a trace in Tempo.
2. Click "Logs for this span" (or similar button).
3. Grafana splits the view and shows Loki logs for that `TraceID`.

*Prerequisite: Application must log `TraceId` in structured logs.*

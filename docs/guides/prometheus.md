# Grafana Prometheus Documentation

## Overview

**Prometheus** is an open-source systems monitoring and alerting toolkit. It collects and stores its metrics as time series data, i.e., metrics information is stored with the timestamp at which it was recorded, alongside optional key-value pairs called labels.

In this project, Prometheus serves as the centralized metrics backend, receiving metrics from all microservices via the OpenTelemetry Collector.

## Architecture

In this Proof of Concept (PoC) environment, Prometheus is deployed in **Monolithic Mode** (Single Binary).

### Deployment Mode: Monolithic
Prometheus runs as a single container (`prom/prometheus`) which handles scraping, storage, and querying.

Key components:
- **Scraper**: Pulls metrics from configured targets (in our case, the OpenTelemetry Collector).
- **TSDB (Time Series Database)**: Stores metrics on disk.
- **PromQL Engine**: Handles queries from Grafana.
- **Alertmanager Integration**: Can forward alerts to Alertmanager (not configured in this PoC).

## Configuration Details

The configuration is defined in [`docker/observability/prometheus.yaml`](file:///d:/Projetos/poc_feature/docker/observability/prometheus.yaml).

### 1. Global Configuration
We follow best practices for scrape intervals and evaluation intervals.

```yaml
global:
  scrape_interval: 15s     # How frequently to scrape targets
  evaluation_interval: 15s # How frequently to evaluate rules
```

### 2. Scrape Configurations
Prometheus scrapes the OpenTelemetry Collector, which acts as a gateway/aggregator for all application metrics.

```yaml
scrape_configs:
  - job_name: 'otel-collector'
    scrape_interval: 10s
    static_configs:
      - targets: ['otel-collector:8889']
```

### 3. Native Histograms (Feature Flag)
We have enabled **Native Histograms** (available in Prometheus v3.0+) to support high-fidelity histogram metrics without the overhead of bucket definitions.

Enabled via CLI flag in `docker-compose.yaml`:
```yaml
command:
  - --enable-feature=native-histograms
```

This allows Prometheus to ingest OTel exponential histograms directly, providing precise quantile calculations (e.g., P99, P99.9) without predefined buckets.

## PromQL (Prometheus Query Language)

PromQL is the query language for Prometheus.

### Basic Queries (Selectors)
Select time series using metric names and labels:
- **By Metric Name:** `http_server_request_duration_seconds_bucket`
- **By Job/Service:** `{job="PoC.Materials"}`
- **Filter by Label:** `http_server_request_duration_seconds_bucket{job="PoC.Materials", status="200"}`

### Aggregators
Aggregate over dimensions:
- **Sum:** `sum(rate(http_server_request_duration_seconds_count[5m]))`
- **By Label:** `sum(rate(http_server_request_duration_seconds_count[5m])) by (job, status)`

### Functions
Common functions for analysis:
- **Rate (Per Second):** `rate(http_server_request_duration_seconds_count[5m])` - Calculates the per-second average rate of increase of the time series in the range vector.
- **Increase (Total Change):** `increase(process_runtime_dotnet_gc_collections_count[1h])` - Calculates the increase in the time series in the range vector.
- **Histogram Quantile:** `histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))` - Calculates the 95th percentile latency (for classic histograms).

## Integration with OpenTelemetry

Prometheus receives metrics via the **OpenTelemetry Collector**.
1. **Application** (Native `Meter` + OTel SDK) -> **OTel Collector** (via OTLP/gRPC).
2. **OTel Collector** processes metrics (batches, adds resource attributes).
3. **OTel Collector** exposes a Prometheus-compatible endpoint (`:8889`).
4. **Prometheus** scrapes the Collector.

**Label Mapping:**
The Collector ensures OTel resource attributes (like `service.name`) are preserved as Prometheus labels (mapped to `job` or `service_name` depending on configuration).

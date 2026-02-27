# Grafana Loki Documentation

## Overview

**Grafana Loki** is a horizontally scalable, highly available, multi-tenant log aggregation system inspired by Prometheus. It is designed to be very cost-effective and easy to operate. It does not index the contents of the logs, but rather a set of labels for each log stream.

In this project, Loki serves as the centralized logging backend, receiving logs from all microservices via the OpenTelemetry Collector.

## Architecture

In this Proof of Concept (PoC) environment, Loki is deployed in **Monolithic Mode** (Single Binary).

### Deployment Mode: Monolithic
All Loki components run within a single container (`grafana/loki`). This simplifies deployment for development and testing while maintaining the same code paths as distributed deployments.

Key components running inside the single binary:
- **Distributor**: Validates incoming logs and shards them to ingesters.
- **Ingester**: Batches logs into chunks and writes them to storage.
- **Querier**: Handles LogQL queries from Grafana.
- **Compactor**: Manages retention and deduplication.

## Configuration Details

The configuration is defined in [`docker/observability/loki.yaml`](file:///d:/Projetos/poc_feature/docker/observability/loki.yaml).

### 1. Ingester & Best Practices
We follow [Grafana Loki Best Practices](https://grafana.com/docs/loki/latest/configure/bp-configure/) for performance and reliability.

```yaml
ingester:
  chunk_target_size: 1536000 # 1.5MB (Target chunk size for better compression/performance)
  chunk_encoding: snappy     # Fast compression
  wal:
    enabled: true            # Write Ahead Log for durability against crashes
    dir: /loki/wal
```

### 2. Storage (Filesystem)
For this PoC, we use the local filesystem to store chunks and index.

```yaml
common:
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1
  ring:
    instance_addr: 127.0.0.1
    kvstore:
      store: inmemory
```

### 3. Limits & Retention
We enforce a 7-day retention policy to manage disk usage in the development environment.

```yaml
limits_config:
  retention_period: 168h      # 7 days
  unordered_writes: true      # Allow out-of-order logs (crucial for distributed systems)
  reject_old_samples: true
  reject_old_samples_max_age: 168h
```

### 4. Compactor
The Compactor is responsible for enforcing retention policies. In this PoC, retention is configured but **disabled by default** to preserve logs during testing.

```yaml
compactor:
  working_directory: /loki/compactor
  compaction_interval: 10m
  retention_enabled: false # Set to true to enable deletion of old logs
  retention_delete_delay: 2h
```

## LogQL (Log Query Language)

LogQL is Prometheus-inspired query language for Loki.

### Basic Queries (Label Matchers)
Select log streams using labels:
- **By Job/Service:** `{job="PoC.Materials"}`
- **By Level:** `{level="error"}`

### Line Filters
Filter the content of log lines:
- **Contains string:** `{job="PoC.Materials"} |= "error"`
- **Does not contain:** `{job="PoC.Materials"} != "debug"`
- **Regex match:** `{job="PoC.Materials"} |~ "error|critical"`

### Parsers & Formatting
Extract structured data from logs:
- **JSON Parser:** `{job="PoC.Materials"} | json`
  - Allows filtering by extracted fields: `{job="PoC.Materials"} | json | latency > 100`
- **Logfmt Parser:** `{job="PoC.Materials"} | logfmt`

### Metric Queries
Generate metrics from logs (e.g., rate of error logs):
`rate({job="PoC.Materials"} |= "error" [1m])`

## Integration with OpenTelemetry

Loki receives logs via the **OpenTelemetry Collector**.
1. **Application** (Native `ILogger` + OTel SDK) -> **OTel Collector** (via OTLP/gRPC).
2. **OTel Collector** processes logs (batches, adds resource attributes).
3. **OTel Collector** exports to **Loki** (via `loki` exporter).

**Label Mapping:**
The Collector is configured to map the OTel `service.name` resource attribute to the Loki `job` label.
```yaml
exporters:
  loki:
    endpoint: "http://loki:3100/loki/api/v1/push"
    default_labels_enabled:
      job: true # Maps service.name -> job
```

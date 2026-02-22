# PoC Feature: Material Formulation System

## Overview
A microservices-based proof-of-concept for managing material formulations, built with **.NET 10**, **AWS Lambda**, **DynamoDB**, and **Modular Clean Architecture**. The system demonstrates advanced patterns for observability, feature management, and local development using **LocalStack**.

## 🚀 Quick Start

### 1. Prerequisites
- Docker Desktop
- **.NET 10 SDK** (specified in `global.json`)
- PowerShell Core (pwsh)

### 2. Start Infrastructure
```bash
docker compose up -d
```

### 3. Deploy Services (LocalStack via Terraform)
```bash
python deployment/localstack/deploy_all_terraform.py
```

### 4. Verify APIs
Run the automated test suite to ensure all services are healthy:
```bash
./scripts/tests/test_all_apis.ps1
```

## 🏗️ Project Structure

The project has been refactored into modular libraries to ensure granular dependencies and high performance.

```text
/src
  /PoC.Materials        # Material management microservice
  /PoC.Costing          # Cost calculation microservice
  /PoC.Populator        # Batch data population microservice
  
  /PoC.Observability    # Centralized OTel configuration (Traces, Metrics, Logs)
  /PoC.FeatureFlags     # Feature Flags via OpenFeature + Unleash
  /PoC.Shared           # Lightweight domain-agnostic contracts & Result patterns
  /PoC.Shared.Infrastructure # Shared Kernel extension & composition root
  
/docker                 # Infrastructure (OTel, Prometheus, Grafana, Unleash)
/deployment             # Master Orchestration Scripts (LocalStack)
/scripts                # Service Pipelines & Automation Utilities
  /config               # Global Environment Configs
  /services             # Per-Service Deployment Pipelines
  /utils                # Shared Automation Helpers
/docs                   # Documentation & Architectural Decisions (ADRs)
```

## 📚 Developer Portal

| Section | Content |
|---|---|
| **[Architecture](docs/architecture/system-overview.md)** | Diagrams, Layers, Data Flow, Concepts |
| **[Automation Framework](docs/guides/automation-framework.md)** | **New:** Detailed guide on the service-isolated CI/CD pipelines. |
| **[Observability](docs/guides/observability.md)** | **Rule:** All OTel code remains in `PoC.Observability`. No Serilog; use Native ILogger. |
| **[Feature Flags](docs/guides/feature-flags.md)** | Usage of `WithFeatureGate` and Unleash integration. |
| **[Decisions (ADRs)](docs/decisions/)** | ADR 004: Modular Shared Libraries (Refactored from monolithic Infra) |
| **[API Specification](docs/openapi.yaml)** | OpenAPI 3.0 specs for all endpoints |

## 🛠️ Core Capabilities

### 🔹 Observability
The system uses **Native OpenTelemetry** for Tracing, Metrics, and Logging. We have moved away from Serilog to reduce overhead and simplify the stack. All telemetry is exported via OTLP to the OpenTelemetry Collector and visualized in Grafana.
- **Trace Context**: Automatically propagated across service calls (including SQS/SNS).
- **Metrics**: Standard and custom business metrics (e.g., `business.costing.value`).
- **Rule**: All observability configuration must stay within the `PoC.Observability` project.

### 🔹 Feature Management
Powered by **Unleash** and **OpenFeature**, allowing granular control over feature rollouts without redeploying code.
- **Fail-safe**: Services use a `FakeUnleash` provider if the API is unreachable in local environments.
- **Gatekeeper**: Minimal API endpoints are protected using the `WithFeatureGate` extension.

### 🔹 Local Development
Fully simulated AWS environment using **LocalStack**. Services are deployed as .NET 10 Lambdas connected to API Gateway (REST API).

## ❓ FAQ

**Q: Why modularize the Shared libraries?**
A: To prevent "dependency bloat". A service should be able to use Observability without pulling in Unleash, or use Shared contracts without pulling in heavy OTel exporters.

**Q: Why no Offset Pagination (Skip/Take)?**
A: DynamoDB scans are expensive. We use **Cursor-based pagination** (Continuous Tokens) for efficient, predictable performance at any scale.

**Q: How do I debug a failed request?**
A: Trace coverage is E2E. Search for the `traceId` in **Grafana Tempo** (`http://localhost:3000`).

---
*Built for High Performance & Observability.*

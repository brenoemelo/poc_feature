# PoC Feature: Material Formulation System

## Overview
A microservices-based proof-of-concept for managing material formulations, built with **.NET 8**, **AWS Lambda**, **DynamoDB**, and **Clean Architecture**.

## 🚀 Quick Start

### 1. Prerequisites
- Docker Desktop
- .NET 8 SDK
- PowerShell Core (pwsh)

### 2. Start Infrastructure
```bash
docker compose -f docker-compose.yml up -d
docker compose -f docker/feature-flags/docker-compose.yaml up -d
docker compose -f docker/observability/docker-compose.yaml up -d
```

### 3. Deploy Services (LocalStack)
```bash
./deployment/localstack/deploy-all.ps1
```

### 4. Verify APIs
Run the automated test suite to ensure all services are healthy:
```bash
./scripts/tests/test_all_apis.ps1
```

## 📚 Developer Portal

| Section | Content |
|---|---|
| **[Architecture](docs/architecture/system-overview.md)** | Diagrams, Layers, Data Flow, Concepts |
| **[Guides](docs/guides/getting-started.md)** | Standards, Observability, Feature Flags |
| **[Operations](docs/operations/troubleshooting.md)** | Troubleshooting Runbooks, Health Checks |
| **[Decisions](docs/decisions/adr-001-microservices-stack.md)** | Architectural Decision Records (ADRs) |

## ❓ FAQ

**Q: Why do we use a Shared Kernel?**
A: To centralize cross-cutting concerns (logging, results, observability) and ensure consistency across microservices, preventing code duplication.

**Q: Why no Offset Pagination (Skip/Take)?**
A: DynamoDB scans are expensive. We use **Cursor-based pagination** (Continuous Tokens) for efficient, predictable performance at any scale.

**Q: How do I debug a failed request?**
A: Use the `traceId` from the error response and search for it in **Grafana Tempo** (`http://localhost:3000`). See the [Observability Guide](docs/guides/observability.md).

**Q: What happens if Feature Flags go offline?**
A: The system is fail-safe. If the GoFeatureFlag container is unreachable, all flags default to `false` (Disabled), and the app remains operational.

## 🏗️ Project Structure
```text
/src                     # Microservices Source Code
/docker                  # Infrastructure (OTel, Feature Flags)
/deployment              # LocalStack Deployment Scripts
/scripts                 # Test & Utility Scripts
/config                  # JSON Payloads & Configurations
/docs                    # Documentation & ADRs
```

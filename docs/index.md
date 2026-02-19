# PoC Feature - Material Formulation System

Welcome to the **Material Formulation System** documentation. This project is a proof-of-concept for managing material formulations in an engineering context, built with a microservices architecture on AWS Lambda/REST API.

## 📚 Documentation Structure

### Architecture Decision Records (ADR)

- [ADR 001: Event-Driven Population Architecture](decisions/001-event-driven-population.md)
- [ADR 002: Costing Engine Architecture](decisions/002-costing-engine-architecture.md)
- [ADR 0001: Use DynamoDB](decisions/0001-use-dynamodb.md)
- [ADR 0002: Use Clean Architecture](decisions/0002-use-clean-architecture.md)
- [ADR 003: Shared Observability Library](decisions/003-shared-observability-library.md)
- [ADR 004: Modular Shared Libraries](decisions/004-modular-shared-libs.md)

### Concepts

- [Gravity Field](architecture/concepts/GravityField.md)
- [Anti-Matter Unit](architecture/concepts/AntiMatterUnit.md)

### Guides

- [Setup Local Environment](guides/getting-started.md)
- [Observability Guide](guides/observability.md)
- [Feature Flags Guide](guides/feature-flags.md)

### API Documentation

- [OpenAPI Specification](openapi.yaml)
- [Insomnia Collection](api/insomnia_antigravity_v1.json)

## 🏗️ System Architecture

The system consists of specialized microservices exposed via **API Gateway**:

1. **PoC.Materials** - Material management and query operations.
2. **PoC.Populator** - Synthetic data generation for testing.
3. **PoC.Costing** - Cost calculation and pricing engine.

### Shared Infrastructure
We use a modular approach for cross-cutting concerns:
- **`PoC.Observability`**: Centralized OTel configuration.
- **`PoC.FeatureFlags`**: Feature management via Unleash.
- **`PoC.Shared`**: Common domain contracts.

## 🚀 Quick Start

```bash
# Start LocalStack
docker-compose up -d

# Deploy Infrastructure & Services
./deployment/localstack/deploy-all.ps1

# Run API Tests
./scripts/tests/test_all_apis.ps1
```

## 📖 Key Features

- **Material Formulation Management** - Create and query material compositions.
- **Pagination & HATEOAS** - Cursor-based navigation.
- [x] Automated Data Population - Generate thousands of test records.
- [x] Native Observability - Centralized monitoring with OpenTelemetry (No Serilog).
- [x] Cost Calculation Engine - Calculate material costs with margin analysis.
- **Event-Driven Architecture** - Decoupled services using SNS/SQS.
- **FluentValidation** - Business rule enforcement.
- [x] Feature Flags - Endpoint toggling with OpenFeature & Unleash.

## 🔗 Related Resources

- [ANTIGRAVITY_RULES.md](../ANTIGRAVITY_RULES.md) - Project coding standards and architectural guidelines.
- **[WALKTHROUGH.md](../../.gemini/antigravity/brain/a2b7b68f-f412-4760-bb1d-4552b7600b71/walkthrough.md)** - Project refactoring walkthrough.

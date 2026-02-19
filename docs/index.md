# PoC Feature - Material Formulation System

Welcome to the **Material Formulation System** documentation. This project is a proof-of-concept for managing material formulations in an engineering context, built with a microservices architecture on AWS Lambda.

## 📚 Documentation Structure

### Architecture Decision Records (ADR)

- [ADR 001: Event-Driven Population Architecture](adr/001-event-driven-population.md)
- [ADR 002: Costing Engine Architecture](adr/002-costing-engine-architecture.md)
- [ADR 0001: Use DynamoDB](adr/0001-use-dynamodb.md)
- [ADR 0002: Use Clean Architecture](adr/0002-use-clean-architecture.md)
- [ADR 003: Shared Observability Library](adr/003-shared-observability-library.md)

### Concepts

- [Gravity Field](concepts/GravityField.md) - Understanding the domain model
- [Anti-Matter Unit](concepts/AntiMatterUnit.md) - Core business entities

### Guides

- [Setup Local Environment](guides/setup-local-environment.md) - Get started with LocalStack
- [Deployment Health Check](guides/deployment-health.md) - Verify deployment status and logs
- [Observability Guide](guides/observability.md) - Monitoring, Tracing, and Logging walkthrough
- [Feature Flags Guide](guides/feature-flags.md) - OpenFeature + Unleash integration

### API Documentation

- [OpenAPI Specification](openapi.yaml) - REST API contract
- [Insomnia Collection](api/insomnia_antigravity_v1.json) - Ready-to-use API requests

## 🏗️ System Architecture

The system consists of three main microservices exposed via a unified **API Gateway**:

1. **PoC.Materials** - Material management and query operations
2. **PoC.Populator** - Synthetic data generation for testing
3. **PoC.Costing** - Cost calculation and pricing engine

All services follow the **Data Sovereignty** principle and communicate via **SNS/SQS** for asynchronous operations.

## 🚀 Quick Start

```bash
# Start LocalStack
docker-compose up -d

# Deploy Infrastructure & Services
./deployment/localstack/deploy-all.ps1

# Run API Tests
./scripts/tests/test_all_apis.ps1

# Run E2E Tests
dotnet test tests/PoC.E2E/PoC.E2E.csproj
```

## 📖 Key Features

- **Material Formulation Management** - Create and query material compositions
- **Pagination & HATEOAS** - Cursor-based navigation for large datasets
- [x] Automated Data Population - Generate thousands of test records
- [x] Vendor-Agnostic Observability - Centralized monitoring with OpenTelemetry & OTLP
- [x] Cost Calculation Engine - Calculate material costs with margin analysis
- **Event-Driven Architecture** - Decoupled services using SNS/SQS
- **FluentValidation** - Business rule enforcement
- **RFC 7807 ProblemDetails** - Standardized error responses
- [x] Feature Flags - Vendor-agnostic endpoint toggling with OpenFeature & Unleash

## 🔗 Related Resources

- [ANTIGRAVITY_RULES.md](../ANTIGRAVITY_RULES.md) - Project coding standards and architectural guidelines

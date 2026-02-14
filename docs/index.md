# PoC Feature - Material Formulation System

Welcome to the **Material Formulation System** documentation. This project is a proof-of-concept for managing material formulations in an engineering context, built with a microservices architecture on AWS Lambda.

## 📚 Documentation Structure

### Architecture Decision Records (ADR)

- [ADR 001: Event-Driven Population Architecture](adr/001-event-driven-population.md)
- [ADR 002: Costing Engine Architecture](adr/002-costing-engine-architecture.md)
- [ADR 0001: Use DynamoDB](adr/0001-use-dynamodb.md)
- [ADR 0002: Use Clean Architecture](adr/0002-use-clean-architecture.md)

### Concepts

- [Gravity Field](concepts/GravityField.md) - Understanding the domain model
- [Anti-Matter Unit](concepts/AntiMatterUnit.md) - Core business entities

### Guides

- [Setup Local Environment](guides/setup-local-environment.md) - Get started with LocalStack

### API Documentation

- [OpenAPI Specification](openapi.yaml) - REST API contract
- [Insomnia Collection](insomnia_collection.json) - Ready-to-use API requests

## 🏗️ System Architecture

The system consists of three main microservices:

1. **PoC.Lambda** - Material management and query operations
2. **PoC.Populator** - Synthetic data generation for testing
3. **PoC.Costing** - Cost calculation and pricing engine

All services follow the **Data Sovereignty** principle and communicate via **SNS/SQS** for asynchronous operations.

## 🚀 Quick Start

```bash
# Start LocalStack
docker-compose up -d

# Build the solution
dotnet build PoC.sln

# Run architecture tests
dotnet test src/PoC.ArchitectureTests
```

## 📖 Key Features

- **Material Formulation Management** - Create and query material compositions
- **Automated Data Population** - Generate thousands of test records
- **Cost Calculation Engine** - Calculate material costs with margin analysis
- **Event-Driven Architecture** - Decoupled services using SNS/SQS
- **FluentValidation** - Business rule enforcement
- **RFC 7807 ProblemDetails** - Standardized error responses

## 🔗 Related Resources

- [ANTIGRAVITY_RULES.md](../ANTIGRAVITY_RULES.md) - Project coding standards and architectural guidelines

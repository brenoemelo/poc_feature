# Material Formulation System - PoC

A proof-of-concept microservices system for managing material formulations in engineering contexts, built with .NET 8, AWS Lambda, and LocalStack.

## 🎯 Project Overview

This system demonstrates a **serverless, event-driven architecture** for managing material compositions, calculating costs, and generating synthetic test data. It follows **Clean Architecture** principles with strict **Data Sovereignty** between microservices.

### Key Features

- ✅ **Material Management** - CRUD operations for material formulations
- ✅ **Cost Calculation Engine** - Calculate material costs with margin analysis
- ✅ **Data Population** - Generate thousands of synthetic materials for testing
- ✅ **Event-Driven Architecture** - Decoupled services using SNS/SQS
- ✅ **FluentValidation** - Business rule enforcement at API boundaries
- ✅ **RFC 7807 ProblemDetails** - Standardized error responses
- ✅ **Architecture Tests** - Automated enforcement of architectural constraints

## 🏗️ Architecture

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│PoC.Materials│     │ PoC.Populator│     │ PoC.Costing │
│             │     │              │     │             │
│ • Materials │     │ • Population │     │ • Prices    │
│ • Query     │     │ • Worker     │     │ • Cost Calc │
│ • Ingestion │     │              │     │             │
└──────┬──────┘     └──────┬───────┘     └──────┬──────┘
       │                   │                    │
       ├───────────────────┴────────────────────┤
       │            PoC.Shared (Domain)         │
       │  • Models  • Events  • Validators      │
       └────────────────────────────────────────┘
              │              │              │
       ┌──────▼──────┐ ┌────▼────┐ ┌───────▼────────┐
       │ materials-  │ │SNS/SQS  │ │costing-prices- │
       │ table (DDB) │ │ Events  │ │table (DynamoDB)│
       └─────────────┘ └─────────┘ └────────────────┘
```

## 📚 Documentation

- **[Full Documentation](docs/index.md)** - Complete documentation index
- **[Setup Guide](docs/guides/setup-local-environment.md)** - Get started with LocalStack
- **[Health Check](docs/guides/deployment-health.md)** - Verify deployments and logs
- **[ADRs](docs/adr/)** - Architecture Decision Records
- **[Domain Concepts](docs/concepts/)** - Business domain documentation
- **[OpenAPI Spec](docs/openapi.yaml)** - REST API contract
- **[ANTIGRAVITY_RULES.md](ANTIGRAVITY_RULES.md)** - Coding standards and guidelines

## 🚀 Quick Start

### Prerequisites

- Docker & Docker Compose
- .NET SDK 8.0
- `sg` command (Linux) or Docker Desktop (Windows/Mac)

### 1. Start LocalStack

```bash
docker-compose up -d
```

### 2. Build the Solution

```bash
dotnet restore PoC.sln
dotnet build PoC.sln
```

### 3. Run Tests

```bash
# Architecture tests
dotnet test src/PoC.ArchitectureTests

# All tests
dotnet test
```

### 4. Deploy to LocalStack

Use the provided scripts to deploy the API Gateway and all microservices:

```powershell
# 1. Deploy API Gateway
./deploy-gateway.ps1

# 2. Deploy Services
./deploy-localstack.ps1           # PoC.Materials
./deploy-localstack-costing.ps1   # PoC.Costing
./deploy-localstack-populator.ps1 # PoC.Populator
```

### 5. Verify Deployment

Run the automated API test script:

```powershell
./test_all_apis.ps1
```

## 🧭 How to Run Locally (Step-by-Step)

- **Prerequisites**
  - Install .NET SDK 8.0
  - Install Docker Desktop and enable Docker Compose
  - Clone the repository to `d:\Projetos\poc_feature`

- **Start Local Infrastructure (LocalStack)**
  - Run `docker-compose up -d` in the project root
  - Confirm that the `poc_feature-localstack-1` container is running

- **Deploy Infrastructure and Services**
  - Run `./deploy-gateway.ps1` to create the API Gateway.
  - Run the service scripts:
    - `./deploy-localstack.ps1` (Materials)
    - `./deploy-localstack-costing.ps1` (Costing)
    - `./deploy-localstack-populator.ps1` (Populator)

- **Test APIs**
  - Run `./test_all_apis.ps1` to validate all endpoints.
  - The script automatically detects the API Gateway ID and runs integration tests.

- **Automated E2E Tests**
  - E2E tests are already configured to use the API Gateway.
  - Run `dotnet test tests/PoC.E2E/PoC.E2E.csproj`

- **Troubleshooting Tips**
  - 500 on Gateway: Verify if deployment scripts were executed in the correct order (Gateway first).
  - 500 on startup: Ensure `TargetFramework=net8.0` and publish target is `linux-x64`
  - Empty DynamoDB: The init script ([init-aws.sh](file:///d:/Projetos/poc_feature/init-aws.sh)) creates tables; validate `materials-table`

> **Note:** It is possible to run the service as a local Lambda via Function URL (recommended) or as a .NET process, provided dependencies point to the LocalStack endpoint.

## 📦 Project Structure

```
poc_feature/
├── src/
│   ├── PoC.Materials/       # Material management microservice
│   ├── PoC.Populator/       # Data generation microservice
│   ├── PoC.Costing/         # Cost calculation microservice
│   ├── PoC.Shared/          # Shared domain models & events
│   └── PoC.ArchitectureTests/ # Architecture constraint tests
├── tests/
│   └── PoC.E2E/             # End-to-end tests
├── docs/                    # Documentation
├── docker-compose.yml       # LocalStack configuration
└── init-aws.sh             # AWS resource initialization
```

## 🔧 Technology Stack

- **.NET 8** - Runtime
- **AWS Lambda** - Serverless compute
- **DynamoDB** - NoSQL database
- **SNS/SQS** - Event messaging
- **LocalStack** - Local AWS simulation
- **FluentValidation** - Input validation
- **NetArchTest** - Architecture testing
- **xUnit** - Unit & E2E testing
- **RestSharp** - HTTP client (E2E tests)

## 📊 Microservices

### PoC.Materials

Material management service with three Lambda functions:

- `poc-materials-api` - Create and query materials (POST/GET /materials)
- `poc-ingestion-worker` - Consume SNS events and persist to DynamoDB

### PoC.Populator

Synthetic data generation service:

- `poc-populator-api` - Accept population requests (POST /populate)
- `poc-populator-worker` - Generate and publish materials via SNS

### PoC.Costing

Cost calculation and pricing service:

- `poc-costing-price-mgmt` - Manage component prices (PUT /prices)
- `poc-costing-engine` - Calculate material costs (POST /calculate-cost)

## 🧪 Testing Strategy

### Architecture Tests

Enforce architectural constraints using NetArchTest:

```bash
dotnet test src/PoC.ArchitectureTests
```

**Rules enforced:**

- Domain (PoC.Shared) has no dependencies on other layers
- Services are independent (no cross-service dependencies)
- Lambda functions follow naming conventions
- Entities are sealed or abstract

### End-to-End Tests

Black-box tests against deployed infrastructure:

```bash
dotnet test tests/PoC.E2E
```

**Coverage:**

- Health checks
- CRUD operations
- Event-driven workflows
- Cost calculations

## 📖 API Endpoints

| Method | Endpoint           | Service    | Description                |
|--------|--------------------|------------|----------------------------|
| GET    | /materials         | Materials  | List all materials         |
| GET    | /materials/{id}    | Materials  | Get material by ID         |
| POST   | /materials         | Materials  | Create material            |
| POST   | /populate          | Populator  | Generate test data         |
| PUT    | /prices            | Costing    | Upsert component price     |
| POST   | /calculate-cost    | Costing    | Calculate material cost    |

## 🎨 Design Principles

1. **Data Sovereignty** - Each service owns its data
2. **Async First** - Event-driven communication via SNS/SQS
3. **Clean Architecture** - Domain-centric design
4. **SOLID Principles** - Single responsibility, dependency inversion
5. **Fail Fast** - Validate early with FluentValidation
6. **RFC 7807** - Standardized error responses

## 🛠️ Development Workflow

1. **Make changes** to code
2. **Build** the solution: `dotnet build`
3. **Run architecture tests**: `dotnet test src/PoC.ArchitectureTests`
4. **Build Docker images** for modified services
5. **Deploy to LocalStack** and test manually or with E2E tests
6. **Update documentation** (ADRs, OpenAPI, Insomnia)

## 📝 Contributing

Please follow the guidelines in [ANTIGRAVITY_RULES.md](ANTIGRAVITY_RULES.md):

- English-only code and comments
- Use records for DTOs
- File-scoped namespaces
- FluentValidation for business rules
- Update ADRs for architectural decisions
- Run architecture tests before committing

## 📄 License

This is a proof-of-concept project for educational purposes.

## 🙏 Acknowledgments

Built following Clean Architecture, DDD, and SOLID principles with inspiration from AWS serverless best practices.

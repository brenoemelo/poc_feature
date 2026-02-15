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
│  PoC.Lambda │     │ PoC.Populator│     │ PoC.Costing │
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
       │ poc-table   │ │SNS/SQS  │ │costing-prices- │
       │ (DynamoDB)  │ │ Events  │ │table (DynamoDB)│
       └─────────────┘ └─────────┘ └────────────────┘
```

## 📚 Documentation

- **[Full Documentation](docs/index.md)** - Complete documentation index
- **[Setup Guide](docs/guides/setup-local-environment.md)** - Get started with LocalStack
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

Follow the detailed steps in [Setup Guide](docs/guides/setup-local-environment.md).

## 🧭 Como Executar Localmente (Passo a Passo)

- Pré-requisitos
  - Instalar .NET SDK 8.0
  - Instalar Docker Desktop e habilitar Docker Compose
  - Clonar o repositório para `d:\Projetos\poc_feature`

- Subir infraestrutura local (LocalStack)
  - Rodar `docker-compose up -d` na raiz do projeto
  - Confirmar que o container `poc_feature-localstack-1` está em execução

- Publicar e criar a Lambda de Materiais
  - Executar o script [deploy-localstack.ps1](file:///d:/Projetos/poc_feature/deploy-localstack.ps1)
  - Esse script:
    - Publica [PoC.Materials.csproj](file:///d:/Projetos/poc_feature/src/PoC.Materials/PoC.Materials.csproj) para Linux (`-r linux-x64`)
    - Cria/atualiza a função Lambda `PoC-Materials`
    - Cria a Function URL pública (AuthType NONE)
  - Ao final, copie a URL exibida (ex.: `http://xxxxx.lambda-url.us-east-1.localhost.localstack.cloud:4566/`)

- Testar a API manualmente
  - Lista de materiais: `GET {FunctionUrl}/materials`
  - Buscar por ID: `GET {FunctionUrl}/materials/{id}`
  - Criar material: `POST {FunctionUrl}/materials` com JSON do DTO

- Configurar testes E2E
  - Atualize o BaseUrl em [appsettings.test.json](file:///d:/Projetos/poc_feature/tests/PoC.E2E/appsettings.test.json) com a Function URL gerada
  - Execute `dotnet test tests/PoC.E2E/PoC.E2E.csproj`

- Dicas de troubleshooting
  - 403 na URL: redeploy com [deploy-localstack.ps1](file:///d:/Projetos/poc_feature/deploy-localstack.ps1) para recriar a Function URL
  - 500 na inicialização: garanta `TargetFramework=net8.0` e publicação para `linux-x64`
  - DynamoDB vazio: o script de init ([init-aws.sh](file:///d:/Projetos/poc_feature/init-aws.sh)) cria tabelas; valide `materials-table`

> Observação: É possível executar o serviço como Lambda local via Function URL (recomendado) ou como processo .NET, desde que as dependências estejam apontando para o endpoint do LocalStack.

## 📦 Project Structure

```
poc_feature/
├── src/
│   ├── PoC.Lambda/          # Material management microservice
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

### PoC.Lambda

Material management service with three Lambda functions:

- `poc-lambda` - Create materials (POST /materials)
- `poc-query` - Query materials (GET /materials, GET /materials/{id})
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
| GET    | /materials         | Lambda     | List all materials         |
| GET    | /materials/{id}    | Lambda     | Get material by ID         |
| POST   | /materials         | Lambda     | Create material            |
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

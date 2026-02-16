# AI Context Map (Architecture & Codebase)

This file provides a high-level overview of the project structure, architecture, and key code patterns for AI assistants.

## 1. Project Overview
- **Stack**: .NET 8, AWS Lambda, API Gateway, DynamoDB.
- **Architecture**: Microservices (Clean Architecture style within each service).
- **Communication**: HTTP (API Gateway) for external access; Shared Libraries for common logic.

## 2. Directory Structure Map

```text
/src
  /PoC.Materials         # [Service] Manages material formulations
    /API/Endpoints       # Minimal API Definitions (Entry Points)
    /Domain/Interfaces   # Repository Contracts & Domain Abstractions
    /Infrastructure      # DynamoDB Implementation
    /Functions           # Lambda Entry Points
    Program.cs           # App Bootstrap & DI

  /PoC.Costing           # [Service] Calculates costs based on prices
    ... (Same structure as Materials)

  /PoC.Populator         # [Service] Seeding tool for data
    /API/Endpoints       # Minimal API Definitions
    /Domain/Services     # Population Strategies
    /Functions           # Lambda Worker (SQS Processor)

  /PoC.Shared            # [Shared Kernel] Common DTOs, Results, Validators
    /Common              # BaseEntity, Result Pattern
    /Models              # Shared DTOs (MaterialFormulation, etc.)
    /Validation          # FluentValidation Extensions
```

## 3. Request Flow (Data Path)

**Example: `POST /api/v1/materials`**

1.  **API Gateway**: Routes request to `PoC-Materials` Lambda.
2.  **Lambda Entry (`Program.cs`)**: Bootstraps ASP.NET Core on Lambda.
3.  **Endpoint (`MaterialsEndpoints.cs`)**:
    - Maps HTTP Verb to Method (`MapPost`).
    - **Validation**: Uses `IValidator<MaterialFormulation>` (FluentValidation).
    - **Execution**: Calls `IMaterialRepository.SaveAsync(input)`.
    - **Response**: Returns `Result<T>` mapped to `IResult` (e.g., `Results.Created` or `Results.Problem`).
4.  **Repository (`DynamoDbMaterialRepository.cs`)**:
    - Persists data to DynamoDB.
    - Uses `DynamoDBContext` or low-level client.

## 4. Key Data Structures (DTOs/Entities)

**MaterialFormulation (Aggregate Root)**
> Located in: `PoC.Shared/Models.cs`
> Used for: API Request/Response & Persistence (currently shared model)

```csharp
public sealed record MaterialFormulation(
    string MaterialId,
    string Name,
    Density? Density,
    List<FormulationComponent> Formulation,
    Dictionary<string, string> Properties,
    int? Version) : IEvent; // Example interface if applicable
```

**Result Pattern (Error Handling)**
> Located in: `PoC.Shared/Common/Result.cs`
> Rule: All Service/Repository methods must return `Result` or `Result<T>`.

```csharp
// Usage
public async Task<Result<MaterialFormulation>> GetByIdAsync(string id) { ... }
```

## 5. Deployment & Infrastructure (IaC)

- **LocalStack**: Used for local emulation (DynamoDB, Lambda, APIGW).
- **Scripts**: PowerShell scripts in `deployment/localstack/`.
  - `gateway.ps1`: Configures API Gateway.
  - `materials.ps1`: Deploys Materials service.
  - `utils.ps1`: Shared helper functions.

## 6. Conventions & Rules

- **Validation**: FluentValidation is required for all write operations.
- **Logging**: Serilog is used; structured logging required (`logger.LogInformation("Processing {Id}", id)`).
- **URLs**: LocalStack API Base URL is static: `http://localhost:4566/restapis/material-api/prod/_user_request_`.

## 7. Pagination & HATEOAS

- **Strategy**: Cursor-based pagination (Forward-only) for efficiency with DynamoDB.
- **Implementation**:
  - **Repository**: Accepts `limit` and `cursor` (Base64 encoded `LastEvaluatedKey`). Returns `PagedResult<T>` with `Cursor`.
  - **API**: Returns `PagedResponse<T>` containing `data`, `meta` (limit, count), and `links` (HATEOAS).
  - **Links**: `self` and `next` (if more pages exist). Generated using `LinkGenerator`.

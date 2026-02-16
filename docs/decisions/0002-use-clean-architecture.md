# ADR 0002: Use Clean Architecture Principles

## Status

Accepted

## Context

The Material Formulation System is a microservices-based application that will evolve over time. We need an architectural pattern that:

- Separates business logic from infrastructure concerns
- Enables independent testing of core domain logic
- Supports multiple deployment targets (Lambda, containers, etc.)
- Facilitates team collaboration with clear boundaries
- Allows technology changes without rewriting business rules

## Decision

We will adopt **Clean Architecture** principles, adapted for serverless Lambda functions.

### Layer Structure

```
┌─────────────────────────────────────┐
│   Presentation (Lambda Functions)   │  ← API Gateway handlers
├─────────────────────────────────────┤
│   Application (Use Cases)           │  ← Business workflows
├─────────────────────────────────────┤
│   Domain (Shared Models & Events)   │  ← Core business entities
├─────────────────────────────────────┤
│   Infrastructure (AWS SDK)          │  ← DynamoDB, SNS, SQS
└─────────────────────────────────────┘
```

### Project Mapping

- **Domain**: `PoC.Shared` - Models, Events, Validators (zero dependencies)
- **Application**: Embedded in Lambda function classes (Use Case logic)
- **Presentation**: Lambda function entry points (`FunctionHandler` methods)
- **Infrastructure**: AWS SDK clients (DynamoDB, SNS, SQS)

### Dependency Rule

**Dependencies point inward**: Infrastructure → Application → Domain

- `PoC.Shared` has NO dependencies on other projects
- `PoC.Lambda`, `PoC.Populator`, `PoC.Costing` depend on `PoC.Shared`
- Services do NOT depend on each other (enforced by architecture tests)

## Consequences

### Positive

- **Testability**: Domain logic can be tested without AWS infrastructure
- **Maintainability**: Clear separation of concerns makes code easier to understand
- **Flexibility**: Can swap DynamoDB for another database by changing infrastructure layer
- **Reusability**: `PoC.Shared` models can be used across all services
- **Enforcement**: `NetArchTest` architecture tests prevent violations

### Negative

- **Complexity**: More projects and abstractions than a monolithic approach
- **Boilerplate**: Requires DTOs and mapping between layers
- **Learning Curve**: Team must understand Clean Architecture principles

## Implementation Notes

- Use **Records** for DTOs in the Domain layer (immutable by default)
- Use **FluentValidation** for business rule validation in the Domain
- Lambda functions act as thin adapters, delegating to use case logic
- Architecture tests run in CI to enforce dependency rules

## Alternatives Considered

1. **Layered Architecture**: Rejected as it allows circular dependencies between layers
2. **Hexagonal Architecture**: Considered equivalent; Clean Architecture chosen for team familiarity
3. **Monolithic Lambda**: Rejected as it violates single responsibility and makes testing harder

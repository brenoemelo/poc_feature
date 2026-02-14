# ADR 003: Refactor PoC.Lambda to PoC.Materials

## Status

Accepted

## Context

The original `PoC.Lambda` project suffered from several issues:

1. **Generic Naming**: "Lambda" describes infrastructure, not business domain
2. **Code Duplication**: DynamoDB client configuration repeated in 3 files
3. **Hardcoded Values**: Table name `"poc-table"` not configurable
4. **Inconsistent Validation**: No input validation in creation function
5. **Inconsistent Error Handling**: Not all functions used RFC 7807 ProblemDetails
6. **Poor Logging**: No structured logging prefixes

These issues violated ANTIGRAVITY_RULES.md and made the codebase harder to maintain.

## Decision

We will refactor `PoC.Lambda` into `PoC.Materials` with the following changes:

### 1. Domain-Driven Naming

- **Project:** `PoC.Lambda` → `PoC.Materials`
- **Functions:** `Function.cs` → `MaterialCreationFunction.cs`
- **Table:** `poc-table` → `materials-table`

### 2. Infrastructure Layer

Create `Infrastructure/` folder with:

- `DynamoDbClientFactory.cs` - Centralized AWS client creation
- `MaterialsConfiguration.cs` - Environment-based configuration

### 3. Validation & Error Handling

- Add FluentValidation to `MaterialCreationFunction`
- Standardize RFC 7807 ProblemDetails across all functions
- Improve error messages with business context

### 4. Structured Logging

Add function name prefixes to all log statements:

```csharp
context.Logger.LogInformation($"[MaterialCreation] Processing material: {material.Name}");
```

### 5. Configuration Management

Use environment variables with sensible defaults:

```csharp
public static string MaterialsTableName => 
    Environment.GetEnvironmentVariable("MATERIALS_TABLE_NAME") ?? "materials-table";
```

## Consequences

### Positive

- **Clarity**: Project name reflects business domain
- **Maintainability**: DRY principle eliminates duplication
- **Flexibility**: Environment-based configuration
- **Robustness**: Input validation prevents bad data
- **Consistency**: All functions follow same patterns
- **Debuggability**: Structured logging aids troubleshooting
- **Compliance**: Adheres to all ANTIGRAVITY_RULES.md

### Negative

- **Breaking Changes**: Lambda function names and table names change
- **Migration Effort**: Requires infrastructure updates and data migration
- **Deployment Complexity**: Must coordinate changes across multiple resources

### Neutral

- **Learning Curve**: Team must learn new naming conventions
- **Documentation Debt**: All docs must be updated

## Implementation Notes

### Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `MATERIALS_TABLE_NAME` | `materials-table` | DynamoDB table for materials |
| `AWS_ENDPOINT_URL` | (LocalStack auto-detect) | AWS service endpoint |
| `LOCALSTACK_HOSTNAME` | `localhost` | LocalStack host |
| `EDGE_PORT` | `4566` | LocalStack port |

### Migration Path

1. Deploy `PoC.Materials` alongside `PoC.Lambda`
2. Create `materials-table` in DynamoDB
3. Optionally migrate data from `poc-table`
4. Update API Gateway routes to point to new functions
5. Verify functionality
6. Deprecate `PoC.Lambda`

### Rollback Plan

- Keep `PoC.Lambda` project until migration is verified
- Both projects can coexist during transition
- Revert API Gateway routes if issues arise

## Alternatives Considered

1. **Keep PoC.Lambda, Only Refactor Internals**
   - **Rejected:** Name still doesn't reflect domain
   - **Rejected:** Doesn't address table naming issue

2. **Gradual Refactoring**
   - **Rejected:** Partial refactoring leaves inconsistencies
   - **Rejected:** Harder to track what's been updated

3. **Create New Project, Delete Old**
   - **Accepted:** Clean break, clear migration path
   - **Accepted:** Forces addressing all issues at once

## References

- ANTIGRAVITY_RULES.md - Project coding standards
- ADR 001: Event-Driven Population Architecture
- ADR 002: Costing Engine Architecture
- REFACTORING_SUMMARY.md - Detailed implementation notes

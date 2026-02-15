# Remediation Plan for ANTIGRAVITY_RULES.md Compliance

This plan outlines the necessary steps to bring the `d:\Projetos\poc_feature` codebase into compliance with the `ANTIGRAVITY_RULES.md` standards.

## 1. Summary of Violations

| ID | Rule | Severity | Status |
|----|------|----------|--------|
| **V1** | **5.1 Project Structure (Clean Architecture)**: The project currently uses a Vertical Slice structure (`PoC.Materials`, `PoC.Costing`) instead of the mandated Layered Architecture (`src/Core`, `src/Application`, `src/Infrastructure`, `src/API`). | High | **Pending** |
| **V2** | **3.4 Result Pattern**: Methods return raw types or `IResult` directly, instead of a `Result<T>` wrapper. | High | **Partially Fixed** (Class created) |
| **V3** | **7.1 Logging Standards**: Structured Serilog logging was missing. | Medium | **Fixed** |
| **V4** | **6.7 Optimistic Locking**: Entities lacked `[DynamoDBVersion]` attribute. | Medium | **Partially Fixed** (Added to MaterialEntity) |
| **V5** | **6.8 Performance**: Usage of `ScanAsync` in repositories. | Low | **Pending** |

## 2. Completed Actions (Proactive Fixes)

The following changes have been applied to the codebase:

1.  **Serilog Implementation (V3)**:
    *   Added `Serilog.AspNetCore` and `Serilog.Sinks.Console` packages to all services.
    *   Configured Serilog in `Program.cs` for `PoC.Materials`, `PoC.Costing`, and `PoC.Populator`.
2.  **Result Pattern Foundation (V2)**:
    *   Created `Result` and `Result<T>` classes in `src/PoC.Shared/Common/Result.cs`.
3.  **Optimistic Locking (V4)**:
    *   Added `[DynamoDBVersion]` property to `MaterialEntity` in `PoC.Materials`.

## 3. Remaining Remediation Steps

### Phase 1: Structural Refactoring (High Effort)
*   **Goal**: Align with Rule 5.1 (Clean Architecture).
*   **Action**: Refactor each service (e.g., `PoC.Materials`) to have explicit internal namespaces or move to separate projects if strict separation is required.
    *   Move Entities and Interfaces to `Core` namespace/folder.
    *   Move Validators, Use Cases (to be created), and DTOs to `Application` namespace/folder.
    *   Move Repositories and AWS implementations to `Infrastructure` namespace/folder.
    *   Keep Controllers/Endpoints in `API` (or `Endpoints` folder).

### Phase 2: Logic Refactoring
*   **Goal**: Adopt Result Pattern and Clean Code principles.
*   **Action**:
    *   Refactor Repositories to return `Result<T>` instead of raw objects or nulls.
    *   Introduce "Use Cases" (MediatR or Services) to encapsulate business logic, removing it from Endpoints.
    *   Update Endpoints to map `Result<T>` to `ProblemDetails`.

### Phase 3: Data Access Optimization
*   **Goal**: Improve DynamoDB performance (Rule 6.8).
*   **Action**:
    *   Review `GetAllAsync` methods. If the table is expected to grow, implement Pagination (Query with LastEvaluatedKey) instead of full Scan.

## 4. Next Steps
*   Approve the structural refactoring plan.
*   Proceed with applying `Result<T>` to `PoC.Materials` repository and endpoints as a pilot.

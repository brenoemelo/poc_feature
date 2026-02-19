# ADR 004: Modular Shared Libraries (Observability & FeatureFlags)

## Status
Accepted

## Context
The previous decision (ADR 003) consolidated observability logic into `PoC.Shared.Infrastructure`. While this reduced code duplication, it had unintended side effects:
1.  **Bloated Dependencies:** Consumers needing only simple shared logic were forced to inherit heavy OpenTelemetry and AWS SDK dependencies.
2.  **Coupling:** Changes to Feature Flag logic (Unleash) would trigger rebuilds/re-testing of Observability components.
3.  **Complexity:** `PoC.Shared.Infrastructure` became a "kitchen sink" for all cross-cutting concerns.

## Decision
We decided to refactor `PoC.Shared.Infrastructure` into specialized, independent libraries:

1.  **`PoC.Observability`**:
    *   **Responsibility:** Centralized OpenTelemetry configuration (Traces, Metrics, Logs).
    *   **Implementation:** Replaced Serilog with native `Microsoft.Extensions.Logging` integrated with OTel.
    *   **Key Dependencies:** `OpenTelemetry.*`, `AWS.Lambda.*`.

2.  **`PoC.FeatureFlags`**:
    *   **Responsibility:** Feature Flag evaluation and provider management.
    *   **Implementation:** OpenFeature with Unleash provider.
    *   **Key Dependencies:** `OpenFeature`, `Unleash.Client`.

3.  **`PoC.Shared.Infrastructure` (Retained)**:
    *   **Responsibility:** Now strictly a lightweight composition root or specific infrastructure helpers that don't fit into the above categories.
    *   **Status:** Significantly reduced in size and complexity.

## Consequences

### Positive
*   **Granular Dependencies:** Microservices can now reference only what they need.
*   **Improved Maintainability:** Observability and Feature Flag logic can evolve independently.
*   **Performance:** Removal of Serilog and usage of native ILogger reduces overhead.
*   **Clean Architecture:** Better separation of concerns aligned with the "screaming architecture" of the project.

### Negative
*   **More Projects:** The solution now has more projects to manage (`PoC.Observability`, `PoC.FeatureFlags`).
*   **Migration Effort:** Existing services had to be updated to reference the new libraries (completed).

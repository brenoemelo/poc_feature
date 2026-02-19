# 01 - Refactoring Strategy: Modular Shared Libraries

## Objective
Decouple the monolithic `PoC.Shared.Infrastructure` project into specialized, independent libraries. This allows new microservices to adopt specific capabilities (e.g., just Observability) without pulling in unrelated dependencies (e.g., Feature Flags or specific persistence patterns).

## Current State
*   **PoC.Shared.Infrastructure**: A "kitchen sink" library containing:
    *   Observability Logic (OTel)
    *   Feature Flag Logic (Unleash)
    *   AWS/LocalStack Helpers
    *   Common behaviors

## Target Architecture

### 1. `PoC.Observability`
*   **Responsibility**: Centralized metrics, traces, and **Logs** (via Native ILogger).
*   **Dependencies**: `OpenTelemetry.*`, `Microsoft.Extensions.Logging`, `AWS.Lambda.*`.
*   **Philosophy**: Zero-external logging libraries. Use `ILogger<T>` everywhere.
*   **Goal**: 100% Native AOT compatible, high performance, automatic TraceId injection via OTel.

### 2. `PoC.FeatureFlags`
*   **Responsibility**: Feature flag evaluation and provider management.
*   **Dependencies**: `OpenFeature`, `Unleash.Client`.
*   **Goal**: Independent feature management that can be reused even in apps that don't need the full observability stack (though they ideally should have it).

### 3. `PoC.Shared` (Core)
*   **Responsibility**: Domain-agnostic contracts, Result patterns, specialized Exceptions.
*   **Dependencies**: Minimal (e.g., `FluentValidation`).

## Benefits
*   **Reduction of Blob**: Smaller deployment artifacts for simpler services.
*   **Clearer Boundaries**: Changes to Observability don't trigger rebuilds of Feature Flag logic.
*   **Reusability**: Easier to package and publish as internal NuGet packages later.

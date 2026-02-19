# PoC Project - Architecture & Coding Guidelines

## 1. Overview & Tech Stack
This project follows an **Event-Driven Microservices Architecture** built upon the .NET ecosystem.
* **Runtime:** Latest Stable .NET (currently .NET 10 / C# 14+). **Always** utilize the most modern language features available.
* **Database:** Amazon DynamoDB (Single Table Design preferred).
* **Messaging:** Amazon SNS (Topics) & Amazon SQS (Queues).
* **Observability:** OpenTelemetry (OTLP), W3C Trace Context.
* **Feature Management:** OpenFeature Standard (Provider: Unleash).
* **Infrastructure:** AWS (LocalStack for Dev) & Docker.
* **Currency:** Always use the latest stable tags for Docker images (prefer `alpine` for size/security) and the latest secure versions of NuGet packages.

## 2. Language & Localization Rules
**Strict Rule:** English is the sole official language of this project.
* **Code:** All class names, methods, variables, and constants must be in English.
* **Comments:** All code comments must be written in English.
* **Commits:** Git commit messages must be in English.
* **Documentation:** PR descriptions, ADRs, and README files must be in English.

## 3. Core Principles
Every line of code must adhere to these principles. Violations are considered immediate technical debt.

### 3.1. Code Quality & Design
* **Clean Code:** Code must be self-explanatory.
* **SOLID Principles:** Strictly enforce SRP and DIP.
* **YAGNI:** Do not implement features "for the future".
* **KISS:** Complexity is a bug. Keep it simple.
* **Comments Policy:**
    * **The "No Comment" Goal:** Code must speak for itself. If you feel the need to write a comment to explain *what* the code does, the code is too complex. **Refactor it instead of commenting.**
    * **Value Only:** Comments are permitted **ONLY** to explain the *Why* behind a non-obvious decision.
    * **No Noise:** Avoid basic comments like `// Loop through items`. These are forbidden.

### 3.2. Validation & Error Handling
* **No Exceptions for Control Flow:** Do not use `try/catch` for business logic validation.
* **FluentValidation:** Use `FluentValidation` libraries in the Application layer.
* **Result Pattern:** Methods should return a `Result<T>` wrapper indicating Success or Failure.
* **API Errors:** All HTTP APIs must return **RFC 7807 ProblemDetails** for 400-500 errors.

### 3.3. High-Scale Engineering Standards
* **Security First:** All implementations must be secure by design. Validate all inputs, sanitize all outputs, and strictly follow the Principle of Least Privilege (IAM/Database).
* **Performance:** Optimize hot paths. Minimize heap allocations. Avoid "Sync-over-Async" at all costs.
* **Scalability:** Services must be stateless and horizontally scalable.
* **Robustness:** Assume dependencies (Database, Network, AWS) will fail. Implement Resilience patterns (Retries, Circuit Breakers, Fallbacks) for **every** external call.

## 4. Microservices Strategy & Boundaries

### 4.1. When to Create a New Service
* **Business Capability:** Services should align with business domains, NOT technical layers.
* **Independence:** A service must be deployable, scalable, and testable in isolation.
* **Coupling Rule:** If two services strictly require each other to be online to function, they should likely be merged.

### 4.2. Data Sovereignty
* **Shared Nothing:** Each microservice owns its own data.
* **No Direct Access:** Service A **MUST NEVER** read/write directly to Service B's database.

### 4.3. Communication Patterns
* **Async First:** Use SNS/SQS for state-changing operations.
* **Synchronous:** Use HTTP/gRPC sparingly (Queries only), wrapped in **Polly Policies**.

### 4.4. Shared Kernel Strategy
* **Reusability:** Common infrastructure logic must be modularized into specialized libraries to avoid bloating the kernel:
    *   `PoC.Shared`: Core domain primitives (Result, IEntity, DTOs). *Zero heavy dependencies.*
    *   `PoC.Observability`: OpenTelemetry & Logging configuration.
    *   `PoC.FeatureFlags`: Feature Management (Unleash).
* **No Domain Leakage:** The Shared Kernel must contain **ONLY** infrastructure concerns. It must never contain business rules or domain entities.

## 5. Internal Service Architecture (.NET 10)

### 5.1. Project Structure (Clean Architecture)
* **src/Core (Domain):** Entities, Interfaces. *Zero dependencies.*
* **src/Application:** Use Cases, Validators, DTOs.
* **src/Infrastructure:** DynamoDB implementation, AWS SDK wrappers.
* **src/API (Presentation):** Controllers, Middleware.

### 5.2. C# Coding Style
* **Primary Constructors:** Use C# 12 Primary Constructors for classes and dependency injection to reduce boilerplate.
* **Records:** Use `record` types for DTOs, Commands, and Events (immutability).
* **Namespaces:** Use `file-scoped namespaces` to reduce indentation.
* **Null Safety:** Avoid `null`. Utilize *Nullable Reference Types* and treat warnings as errors.

## 6. Data Persistence (DynamoDB)
* **Single Responsibility:** Repositories handle data access only.
* **Optimistic Locking:** Use Version Numbers.
* **Performance:** Prefer `Query`. Avoid `Scan`.

## 7. Logging & Observability Standards
The system must be fully observable via **OpenTelemetry (OTLP)** and **Vendor Agnostic**.

### 7.1. OTLP & Vendor Neutrality
* **Protocol:** All telemetry (Logs, Metrics, Traces) must be exported via **OTLP** (gRPC or HTTP/Protobuf).
* **No Vendor SDKs:** Do not use proprietary SDKs inside the application code.
* **Configuration:** Endpoints and Headers must be configurable via `appsettings.json` to allow switching backends without code changes.

### 7.2. Metrics & Warm-up
* **Startup Tracking:** All services must measure and emit `app.startup_duration_ms` to monitor Cold Starts.
* **Runtime Stats:** `AddRuntimeInstrumentation` must be enabled to track JIT, GC, and ThreadPool usage.

### 7.3. Distributed Tracing
* **W3C Standard:** Use **W3C Trace Context** for propagation.
* **Correlation:** Ensure `TraceId` is injected into Logs (via `Activity.Current` or `ILogger` scopes) and HTTP Response Headers for easier debugging.
* **AWS Instrumentation:** Must instrument AWS SDK calls (DynamoDB, SNS, SQS) to visualize the full dependency chain.

### 7.4. Logging Standards (ILogger API)
We strictly use the native `Microsoft.Extensions.Logging.ILogger` integrated directly with the OpenTelemetry Logger Provider via `PoC.Observability`.
* **No Serilog:** Serilog has been **REMOVED** to reduce overhead and align with Cloud-Native standards. Do not add it back.
* **Structured Logging (No Interpolation):** It is **STRICTLY FORBIDDEN** to use string interpolation (`$""`) in log messages. Always use message templates to preserve property names for querying in backends like Loki/Elasticsearch.
    * 🔴 *Bad:* `_logger.LogInformation($"User {userId} processed order {orderId}.");`
    * 🟢 *Good:* `_logger.LogInformation("User {UserId} processed order {OrderId}.", userId, orderId);`
* **High-Performance Logging (Source Generators):** For hot paths or highly frequent operations, you **MUST** use the `[LoggerMessage]` attribute to generate compile-time logging methods, avoiding boxing and allocation overhead.
* **Semantic Log Levels:**
    * `Trace` / `Debug`: Detailed flow and troubleshooting. Disabled in Production.
    * `Information`: Significant business events (e.g., "OrderCreated", "PaymentProcessed"). Do not log every step of a function.
    * `Warning`: Handled exceptions, retries (e.g., Polly retry), or unexpected but recoverable states.
    * `Error`: Unhandled exceptions or failures that interrupt a business flow. (Triggers alerts).
    * `Critical`: System crashes, infrastructure offline, out of memory. (Wakes up the SRE team).
* **Scoping (`BeginScope`):** Use `_logger.BeginScope` to attach ambient context (e.g., `TenantId`, `CorrelationId`) to a block of code, ensuring all inner logs inherit these properties.
* **Security & Compliance:** It is **STRICTLY FORBIDDEN** to log Personally Identifiable Information (PII), passwords, secrets, or raw API tokens.

## 8. Version Control & Commits
* **Conventional Commits:** Follow the standard (e.g., `feat(cart): add item limit`).
* **Branching:** Trunk Based Development or Short-lived Feature Branches.

## 9. Documentation Standards
Documentation is treated as code.
* **Knowledge Base:** Use Obsidian-friendly Markdown in `/docs`.
* **API:** Keep OpenAPI (Swagger) and Insomnia Collections updated in every PR.

## 10. Automated Enforcement
* **Architecture Tests:** Use `NetArchTest` to enforce layer dependencies.
* **Format:** Use `.editorconfig` to enforce coding styles automatically.

## 11. End-to-End (E2E) Testing Strategy
* **Stack:** xUnit + RestSharp + FluentAssertions.
* **Scope:** Black Box Testing of running endpoints.
* **Environment:** Must use external configuration (`appsettings.test.json`). No Mocks allowed.
* **Observability Testing:** E2E tests must verify if Traces are being emitted using a Mock Collector (e.g., WireMock) to validate the `TraceId` propagation.

## 12. Repository Hygiene & Scratchpad Protocol
* **The Scratchpad (`/scratchpad`):**
    * **Purpose:** This folder is the **ONLY** allowed place for temporary files, draft notes, or raw LLM outputs.
    * **Git Rule:** The `/scratchpad` folder must be included in `.gitignore`. Files inside it are never committed.
* **Cleanup:** Auxiliary files used during development **MUST be deleted** before merging.

## 13. Deployment & Automation Scripts
Scripts used to provision AWS resources (local or remote) must be robust, idempotent, and self-documenting.

### 13.1. Safety & Pre-flight Checks
* **Fail Fast:** The script must immediately stop if any command fails (`set -euo pipefail`).
* **Connection Check:** Verify AWS connectivity before execution.

### 13.2. Idempotency & Cleanup
* **Clean Slate Strategy:** Check if resource exists -> Delete (if testing) -> Create.
* **Retry Pattern:** Wrap AWS CLI commands in a retry loop to handle eventual consistency.

## 14. REST API Design Guidelines
We follow **Pragmatic REST** standards (Richardson Maturity Model).

### 14.1. Naming & Verbs
* **Nouns:** `/api/products` (Plural, Kebab-case).
* **Verbs:** Use correct HTTP methods (`GET`, `POST`, `PUT`, `DELETE`, `PATCH`).

### 14.2. Status Codes
* **No Soft Failures:** Never return `200 OK` with an error body. Use `4xx` or `5xx`.

### 14.3. Pagination Strategy (Cursor-based)
* **No Offset Pagination:** "Skip/Take" is **STRICTLY FORBIDDEN** for DynamoDB.
* **Cursor Pattern:** Use **Forward-Only Pagination** via Continuation Tokens (`?limit=10&cursor=Base64Token`).

### 14.4. HATEOAS (Hypermedia)
* **Navigability:** Responses must include a `links` array guiding the client to the next actions.
* **Envelope:** Use a standard wrapper:
    ```json
    {
      "data": [ ... ],
      "meta": { "nextCursor": "..." },
      "links": [ { "rel": "next", "href": "..." } ]
    }
    ```

## 15. AI Collaboration Standards
To maximize AI assistant efficiency (Trae, Cursor, Copilot), we maintain specific context files.

### 15.1. Context-as-Code
* **The Map:** Maintain a `.ai-context.md` (or `ARCHITECTURE_MAP.md`) file describing the current high-level structure and data flow.
* **Updates:** This file must be updated whenever a new service is added.

### 15.2. Prompting Strategy
* **Plan Before Code:** Ask the AI to "Plan first, then implement".
* **Spec-First:** For complex logic, ask the AI to generate a Gherkin Spec or a Checklist before writing C#.

### 15.3. MCP & Context Retrieval
* **Context Check:** The AI must always consult the `Personal Context` (Context 7) or equivalent Model Context Protocol tools if available, to ensure the solution aligns with the latest unwritten constraints or user preferences.
* **Modernity Check:** Before implementing, verify if the proposed solution uses the most current stable libraries and patterns available in the context.

## 16. Feature Flags (OpenFeature)
Decouple deployment from release using the **OpenFeature** standard.

### 16.1. Implementation Standard
* **Provider:** Use **Unleash** (Self-Hosted via Docker) as the backend provider.
* **Clean Code:** Avoid polluting Controllers with `if (feature.IsEnabled)`.
* **Attribute-Based:** Use the `[FeatureGate("flag-key")]` attribute to secure endpoints.

### 16.2. Lifecycle Management
* **Debt:** Feature flags are technical debt. Once a feature is stable, the flag and the attribute **MUST** be removed.
* **UI:** Manage flags via the **Unleash Dashboard**. Do not hardcode values in C#.
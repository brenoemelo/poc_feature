# Antigravity Project - Architecture & Coding Guidelines

## 1. Overview & Tech Stack
This project follows an **Event-Driven Microservices Architecture** built upon the .NET 8 ecosystem.
* **Runtime:** .NET 8 (C# 12)
* **Database:** Amazon DynamoDB (Single Table Design preferred)
* **Messaging:** Amazon SNS (Topics) & Amazon SQS (Queues)
* **Observability:** OpenTelemetry & Serilog
* **Infrastructure:** AWS

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
* **YAGNI (You Aren't Gonna Need It):** Do not implement features or abstractions based on "what if". Implement only what is necessary for current requirements.
* **KISS (Keep It Simple, Stupid):** Complexity is a bug. Keep it simple.

### 3.2. Validation & Error Handling
* **No Exceptions for Control Flow:** Do not use `try/catch` for business logic validation.
* **FluentValidation:** Use `FluentValidation` libraries in the Application layer.
* **Result Pattern:** Methods should return a `Result<T>` wrapper indicating Success or Failure, rather than throwing exceptions.
* **API Errors:** All HTTP APIs must return **RFC 7807 ProblemDetails** for 400-500 errors.

## 4. Microservices Strategy & Boundaries

### 4.1. When to Create a New Service
Do not create a new service just for the sake of it. Follow these boundaries:
* **Business Capability:** Services should align with business domains (e.g., `OrderService`, `PaymentService`), NOT technical layers (e.g., `DataService`, `ValidationService`).
* **Independence:** A service must be deployable, scalable, and testable in isolation.
* **Coupling Rule:** If two services strictly require each other to be online to function, they should likely be merged into one.

### 4.2. Data Sovereignty (The Golden Rule)
* **Shared Nothing:** Each microservice owns its own data and database.
* **No Direct Access:** Service A **MUST NEVER** read/write directly to Service B's database.
* **Data Sharing:** If Service A needs data from Service B, it must:
    1. Call Service B's API (Synchronous - use sparingly).
    2. Subscribe to Service B's Events and replicate the necessary data locally (Event Carried State Transfer - Preferred).

### 4.3. Communication Patterns
* **Async First (Smart Endpoints, Dumb Pipes):**
    * Use **SNS/SQS** for all state-changing operations between services.
    * Producer (SNS) emits an event (`OrderPlaced`).
    * Consumer (SQS) reacts to the event (`InventoryService` reduces stock).
* **Synchronous (HTTP/gRPC):**
    * Use only for **Queries** or explicitly blocking operations where the user needs an immediate answer.
    * **Resilience:** All HTTP calls between services must be wrapped in **Polly Policies** (Retry, Circuit Breaker, Timeout).

## 5. Internal Service Architecture (.NET 8)

### 5.1. Project Structure (Clean Architecture)
* **src/Core (Domain):** Entities, Interfaces, Value Objects. *Zero dependencies.*
* **src/Application:** Use Cases, Validators, DTOs.
* **src/Infrastructure:** DynamoDB implementation, AWS SDK wrappers.
* **src/API (Presentation):** Controllers, Middleware.

### 5.2. C# Coding Style
* Use **Records** for DTOs, Commands, and Events.
* Use `file-scoped namespaces`.
* Avoid `null`. Utilize *Nullable Reference Types* and treat warnings as errors.

## 6. Data Persistence (DynamoDB)
* **Single Responsibility:** Repositories handle data access only.
* **Optimistic Locking:** Use Version Numbers to prevent data overwrites in concurrent scenarios.
* **Performance:** Prefer `Query` operations. Avoid `Scan` at all costs.

## 7. Logging & Observability Standards
Logging is not for debugging on your machine; it is for understanding system behavior in production.

* **Structured Logging:** Text-based logs are forbidden. All logs must be structured JSON using **Serilog**.
    * *Bad:* `Log.Info("User " + userId + " created order " + orderId);`
    * *Good:* `Log.Info("User {UserId} created Order {OrderId}", userId, orderId);`
* **Correlation ID:** Every log entry must include a `TraceId` or `CorrelationId`.
    * **HTTP:** Middleware must extract or generate this ID.
    * **SQS/SNS:** The ID must be passed in message attributes and manually attached to the logging context in the consumer.
* **Log Levels:**
    * *Debug:* Detailed flows (enabled only when troubleshooting).
    * *Information:* High-level flow events.
    * *Warning:* Unexpected but handled issues.
    * *Error:* Exceptions or unhandled states requiring human intervention.
* **Security (Redaction):** **STRICTLY FORBIDDEN** to log PII (Personally Identifiable Information), passwords, tokens, or secrets.

## 8. Version Control & Commits
* **Conventional Commits:** We follow the Conventional Commits specification.
    * `feat(shopping-cart): add item limit`
    * `fix(payment): resolve currency conversion bug`
    * `chore: update nuget packages`
* **Branching:** Use Trunk Based Development or Short-lived Feature Branches.

## 9. Documentation Standards
Documentation is treated as code. It lives in the repository and must be updated in the same Pull Request as the code changes.

### 9.1. Knowledge Base (Obsidian)
The `/docs` folder is an Obsidian Vault.
* **Format:** All documentation must be in Markdown (`.md`).
* **Linking:** Use **WikiLinks** (`[[Concept Name]]`) to connect related documents. Avoid absolute paths.
* **Structure:**
    * `/docs/adr`: Architecture Decision Records (Why we chose X over Y).
    * `/docs/guides`: Developer onboarding and "How-to" guides.
    * `/docs/concepts`: Explanations of business domains (Ubiquitous Language).
* **Audience:** Developers. Keep it technical, concise, and focused on "How" and "Why".

### 9.2. API Documentation
* **OpenAPI (Swagger):**
    * Must be auto-generated from code annotations.
    * XML Comments (`/// <summary>`) are **mandatory** for all Controllers and DTOs to ensure the Swagger UI is descriptive.
* **Insomnia:**
    * An `insomnia_collection.json` (or similar workspace file) must be maintained in the root or `/docs` folder.
    * **Rule:** If you add or modify an endpoint, you **MUST** update the Insomnia export in the same PR.

## 10. Automated Enforcement
* **Architecture Tests:** Use `NetArchTest` to enforce layer dependencies (e.g., "Domain cannot reference Infrastructure"). These tests must run in the CI pipeline.
* **Format:** Use `.editorconfig` to enforce coding styles automatically on save.

## 11. End-to-End (E2E) Testing Strategy
The purpose of E2E tests is to verify the running application from the perspective of an external consumer (Black Box Testing).

* **Technology Stack:** Tests must be written in C# using **xUnit**, **RestSharp**, and **FluentAssertions**.
* **Separation:** E2E tests reside in the `tests/PoC.E2E` project, separate from Unit and Integration tests.
* **Environment Agnostic:** Tests must not have hardcoded URLs. Use `appsettings.test.json` or Environment Variables to configure the `BaseUrl`.
* **No Mocking:** E2E tests interact with the **real** deployed infrastructure (API + DynamoDB + SQS). Mocks are strictly forbidden here.
* **Safe Data:**
    * **Read-Only Tests:** Safe to run anytime.
    * **Write Tests:** Must generate their own unique test data (randomized IDs/names) to avoid colliding with real data.
    * **Teardown:** Ideally, tests should clean up the data they created via API calls (DELETE), though in a chaotic environment, data is assumed "dirty".
* **Health Checks:** Every microservice must have a `/health` endpoint covered by an E2E test.

---
**Pull Request Review Checklist:**
- [ ] Is the language strictly English?
- [ ] Does this change belong in this Microservice?
- [ ] Is data sovereignty respected?
- [ ] Are logs structured (Serilog) and free of PII?
- [ ] Is the `CorrelationId` properly propagated?
- [ ] Is the code Idempotent?
- [ ] **Is the Documentation (Obsidian/ADR) updated?**
- [ ] **Is the Insomnia collection updated with new endpoints?**
- [ ] Are Architecture Tests passing?
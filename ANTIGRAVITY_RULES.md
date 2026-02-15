# PoC Project - Architecture & Coding Guidelines

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

## 5. Internal Service Architecture (.NET 8)

### 5.1. Project Structure (Clean Architecture)
* **src/Core (Domain):** Entities, Interfaces. *Zero dependencies.*
* **src/Application:** Use Cases, Validators, DTOs.
* **src/Infrastructure:** DynamoDB implementation, AWS SDK wrappers.
* **src/API (Presentation):** Controllers, Middleware.

### 5.2. C# Coding Style
* Use **Records** for DTOs, Commands, and Events.
* Use `file-scoped namespaces`.
* Avoid `null`. Utilize *Nullable Reference Types*.

## 6. Data Persistence (DynamoDB)
* **Single Responsibility:** Repositories handle data access only.
* **Optimistic Locking:** Use Version Numbers.
* **Performance:** Prefer `Query`. Avoid `Scan`.

## 7. Logging & Observability Standards
* **Structured Logging:** Logs must be structured JSON using **Serilog**.
* **Correlation ID:** Every log entry must include a `TraceId` or `CorrelationId`.
* **Security:** **STRICTLY FORBIDDEN** to log PII or secrets.

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

## 12. Repository Hygiene & Scratchpad Protocol
* **The Scratchpad (`/scratchpad`):**
    * **Purpose:** This folder is the **ONLY** allowed place for temporary files, draft notes, or raw LLM outputs.
    * **Git Rule:** The `/scratchpad` folder must be included in `.gitignore`. Files inside it are never committed.
* **Cleanup:** Auxiliary files used during development **MUST be deleted** before merging.
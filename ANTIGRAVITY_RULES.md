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

## 13. Deployment & Automation Scripts
Scripts used to provision AWS resources (local or remote) must be robust, idempotent, and self-documenting.

### 13.1. Safety & Pre-flight Checks
* **Fail Fast:** The script must immediately stop if any command fails.
    * *Bash:* Use `set -euo pipefail`.
    * *PowerShell:* Use `$ErrorActionPreference = "Stop"`.
* **Connection Check:** Before attempting any logic, the script must verify connectivity to the AWS provider (e.g., `aws sts get-caller-identity`). If this fails, abort immediately.
* **Environment Safety:** Destructive scripts (cleanup/re-create) must explicitly check if the target environment is PRODUCTION. If so, they must abort or require manual confirmation.

### 13.2. Idempotency & Cleanup (The "Clean Slate" Strategy)
* **Check-Delete-Create:**
    1.  Check if the resource exists.
    2.  If exists, delete it (and wait for deletion to complete).
    3.  Create the new resource.
* **Dependency Handling:** When deleting a resource (e.g., DynamoDB Table), ensure dependent triggers (e.g., Lambda Event Source Mappings) are removed first to avoid "ResourceInUse" errors.

### 13.3. Resilience (Retries)
* **Eventual Consistency:** AWS is eventually consistent. A command to create a resource might succeed, but the resource might not be ready immediately.
* **Retry Pattern:** All AWS CLI commands (Create, Delete, Update) must be wrapped in a `retry` function that attempts the operation at least 3 times with exponential backoff.

### 13.4. Logging & Documentation
* **Structured Output:** Use colors to denote steps:
    * GREEN: Success / Completion.
    * YELLOW: Warning / Retrying / Waiting.
    * RED: Critical Failure.
* **Self-Documenting:** Use comments as documentation. Each major block (e.g., "Create SQS") must have a header comment explaining *why* it is needed.

## 14. REST API Design Guidelines
We follow **Pragmatic REST** standards. The API must be predictable, resource-oriented, and use standard HTTP mechanics.

### 14.1. Resource Naming (URIs)
* **Nouns, not Verbs:** URIs represent resources (things), not actions.
    * *Bad:* `POST /api/create-product`, `GET /api/get-all-users`
    * *Good:* `POST /api/products`, `GET /api/users`
* **Pluralization:** Always use **plural nouns** for consistency.
    * `GET /api/orders` (Collection)
    * `GET /api/orders/{id}` (Single Resource)
* **Kebab-case:** Use lowercase and hyphens for URLs.
    * *Bad:* `/api/UserProfile`, `/api/user_profile`
    * *Good:* `/api/user-profiles`
* **Hierarchy:** Use nesting to show relationships, but avoid going deeper than 2 levels.
    * *Good:* `/api/customers/{id}/orders` (Orders belonging to a customer)
    * *Too Deep:* `/api/customers/{id}/orders/{orderId}/items/{itemId}` (Prefer flat: `/api/order-items/{itemId}`)

### 14.2. HTTP Methods (Semantics)
You must use the correct verb for the action.

| Verb | Action | Idempotent? | Success Status | Failure Status |
| :--- | :--- | :--- | :--- | :--- |
| **GET** | Read a resource or collection. Never modifies state. | YES | `200 OK` | `404 Not Found` |
| **POST** | Create a new resource. | NO | `201 Created` (Must return `Location` header) | `400 Bad Request`, `422 Unprocessable` |
| **PUT** | **Replace** a resource entirely. If a field is missing, it is set to null. | YES | `200 OK` or `204 No Content` | `404 Not Found` |
| **PATCH** | **Partial Update**. Only fields sent are updated. | NO* | `200 OK` | `404 Not Found`, `400 Bad Request` |
| **DELETE** | Remove a resource. | YES | `204 No Content` | `404 Not Found` |

*(Note on PATCH: Ideally idempotent, but technically not guaranteed by spec. Treat with care).*

### 14.3. Status Codes (The Contract)
* **The "200 OK with Error" Anti-Pattern:**
    * **STRICTLY FORBIDDEN:** Returning `200 OK` with a body like `{"error": "Validation Failed"}`.
    * If the request failed, the HTTP Status Code **MUST** reflect the failure (4xx or 5xx).
* **Common Codes to Use:**
    * `200 OK`: Standard success (synchronous).
    * `201 Created`: Resource created successfully.
    * `202 Accepted`: Request received for background processing (Async).
    * `204 No Content`: Successful action with no body to return (DELETE/PUT).
    * `400 Bad Request`: Malformed syntax.
    * `401 Unauthorized`: Missing or invalid token.
    * `403 Forbidden`: Token valid, but user lacks permission.
    * `404 Not Found`: Resource does not exist.
    * `422 Unprocessable Entity`: Business validation failed (e.g., "Email already exists").
    * `500 Internal Server Error`: Unhandled exception (Bug).

### 14.4. Filtering, Sorting, and Pagination
Do not create new endpoints for filtering. Use **Query Parameters**.

* **Filtering:** `GET /api/products?category=electronics&status=active`
* **Sorting:** `GET /api/products?sort=-created_at` ( `-` implies descending, `+` or none implies ascending).
* **Pagination:**
    * Use `page` and `pageSize` (or `limit`/`offset`).
    * Default `pageSize` should be reasonable (e.g., 20).
    * Responses must include pagination metadata (Total items, Total pages).

### 14.5. Versioning
* **URI Versioning:** All public endpoints must be versioned.
* **Pattern:** `/api/v{number}/resource`
    * Example: `/api/v1/payments`
* **Breaking Changes:** Never introduce breaking changes to an existing version. Create `/api/v2/payments` instead.

## 15. AI Collaboration Standards
To maximize AI assistant efficiency (Trae, Cursor, Copilot), we maintain specific context files.

### 15.1. The AI Context Map (`.ai-context.md`)
* **Purpose:** A high-level technical summary of the system state, specifically optimized for LLM token efficiency.
* **Content:**
    * Current list of Microservices and their ports.
    * Simplified Entity-Relationship diagrams (text-based).
    * Key architectural constraints (e.g., "Always use Result<T>", "No direct DB access").
* **Maintenance:** This file must be updated when a new service or major architectural pattern is introduced.

### 15.2. Prompting Strategy (CoT)
* **Chain of Thought:** When requesting complex changes, explicitly ask the AI to "Plan first, then implement".
    * *Prompt Pattern:* "Read @POC_RULES.md. Plan the implementation of [Feature] creating a checklist of files to modify. Wait for my approval before writing code."
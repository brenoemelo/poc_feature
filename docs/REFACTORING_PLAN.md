# Refactoring Plan — Strict Code Review Audit

> **Reviewer**: Senior Principal .NET Architect
> **Date**: 2026-02-16
> **Scope**: Full solution (`src/`) against [ANTIGRAVITY_RULES.md](file:///d:/Projetos/poc_feature/ANTIGRAVITY_RULES.md) and .NET 8 / C# 12 best practices.
> **Files Audited**: 73 `.cs` source files across 6 projects.

---

## 🚨 Critical Blockers (Must Fix Immediately)

Issues that violate core architectural rules, create security risks, or will cause production failures.

| # | File | Issue | Rule Violation | Recommended Fix |
| :--- | :--- | :--- | :--- | :--- |
| **C1** | [PopulationStrategies.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/PopulationStrategies.cs) | **Shared Kernel contains Infrastructure concerns.** `HttpClient` calls live inside `PoC.Shared` (the Domain Kernel). This drags HTTP I/O into the Domain layer, violating "Zero Dependencies" on external infra. | Rule 5.1 — `src/Core (Domain): Zero dependencies.` | Move `PopulationStrategies.cs`, `PopulationContext`, and `IPopulationStrategy` into `PoC.Populator.Infrastructure` or a dedicated `PoC.Populator.Domain.Services` layer. The Shared Kernel must contain only pure domain logic. |
| **C2** | [PoC.Shared.csproj](file:///d:/Projetos/poc_feature/src/PoC.Shared/PoC.Shared.csproj) | **Domain Kernel references `Bogus` (test fake-data library).** A runtime NuGet dependency on a data-generation library in the Domain Kernel. This is a test concern leaking into production assemblies. | Rule 5.1, SOLID (SRP) | Remove `Bogus` from `PoC.Shared.csproj`. The `PopulationStrategies.cs` file that uses it should move to `PoC.Populator` (see C1). |
| **C3** | [DynamoDbMaterialRepository.cs:19](file:///d:/Projetos/poc_feature/src/PoC.Materials/Infrastructure/Persistence/DynamoDbMaterialRepository.cs#L19) | **`GetAllAsync` uses DynamoDB `Scan`.** Full table scans are O(n) and consume all provisioned RCUs. This is a **critical performance risk** for any table over a few thousand items. | Rule 6 — `Prefer Query. Avoid Scan.` | Redesign the data model to support a GSI (e.g., `GSI-PK: "MATERIAL"`, `GSI-SK: material_id`) and use `QueryAsync` instead. Alternatively, accept the trade-off and document it as an ADR, since "list all" on a partition-key-only table inherently requires a scan. |
| **C4** | [DynamoDbMaterialRepository.cs:89](file:///d:/Projetos/poc_feature/src/PoC.Materials/Infrastructure/Persistence/DynamoDbMaterialRepository.cs#L89) | **`GetCountAsync` uses DynamoDB `Scan` with `Select.COUNT`.** Even with `Select.COUNT`, the entire table is traversed, burning RCUs. | Rule 6 — `Avoid Scan.` | Maintain a counter in a separate DynamoDB item (atomic counter pattern) or use `DescribeTable` for an approximate count. |
| **C5** | [PopulationStrategies.cs:129](file:///d:/Projetos/poc_feature/src/PoC.Shared/PopulationStrategies.cs#L129) | **Bare `catch` swallows all exceptions silently.** In `EnsurePricesPopulationStrategy`, errors from the HTTP call are caught with an empty `catch` block, hiding failures completely. | Rule 3.2 — No Exceptions for Control Flow; Observability Rule 7 | At minimum, log the exception. Ideally, return a `Result.Failure` to propagate the error to the caller. |
| **C6** | [IMaterialsClient.cs](file:///d:/Projetos/poc_feature/src/PoC.Costing/Infrastructure/ExternalServices/IMaterialsClient.cs) | **Interface defined in Infrastructure, not Domain.** The `IMaterialsClient` interface lives in `PoC.Costing.Infrastructure.ExternalServices` instead of `PoC.Costing.Domain.Interfaces`. This inverts the Clean Architecture dependency rule — the domain cannot reference this contract without referencing Infrastructure. | Rule 5.1 — Clean Architecture (DIP) | Move `IMaterialsClient` to `PoC.Costing.Domain.Interfaces`. Keep the implementation in Infrastructure. |
| **C7** | [.gitignore](file:///d:/Projetos/poc_feature/.gitignore) | **`/scratchpad` folder is NOT in `.gitignore`.** Rule 12 explicitly requires it. Risk of committing temporary/sensitive files. | Rule 12 — Repository Hygiene | Add `/scratchpad` to `.gitignore`. |
| **C8** | No `.editorconfig` at solution root | **Missing `.editorconfig`.** Rule 10 mandates using `.editorconfig` to enforce coding styles automatically. Without it, there is no automated style enforcement across the team. | Rule 10 — Automated Enforcement | Create a root `.editorconfig` with project-standard rules (indentation, naming, severity levels for analyzers). |

---

## ⚠️ Major Improvements (Tech Debt)

Issues that affect maintainability, performance, or strict rule adherence.

---

### 1. `Console.WriteLine` Debug Logging (Rule 7 — Structured Logging)

All structured logging must go through **Serilog**, never `Console.WriteLine`. These bypass structured JSON formatting, correlation IDs, and OTLP export.

**Offending files:**
| File | Line | Content |
| :--- | :--- | :--- |
| [DynamoDbCostingRepository.cs](file:///d:/Projetos/poc_feature/src/PoC.Costing/Infrastructure/Persistence/DynamoDbCostingRepository.cs#L17) | 17, 28 | `Console.WriteLine($"[UpsertPriceAsync] Processing {request.ComponentName}...")` |
| [Program.cs (Materials)](file:///d:/Projetos/poc_feature/src/PoC.Materials/Program.cs#L17) | 17 | `Console.WriteLine("STARTING UP PoC.Materials...")` |
| [Program.cs (Costing)](file:///d:/Projetos/poc_feature/src/PoC.Costing/Program.cs#L19) | 19 | `Console.WriteLine("STARTING UP PoC.Costing...")` |
| [Program.cs (Populator)](file:///d:/Projetos/poc_feature/src/PoC.Populator/Program.cs#L15) | 15 | `Console.WriteLine("STARTING UP PoC.Populator...")` |

**Fix:** Replace all `Console.WriteLine` with `Log.Information(...)` or `logger.LogInformation(...)`. The `DynamoDbCostingRepository` should inject `ILogger<DynamoDbCostingRepository>` and use structured templates:

```csharp
// ❌ Before
Console.WriteLine($"[UpsertPriceAsync] Processing {request.ComponentName}. Existing: {existing != null}");

// ✅ After
_logger.LogDebug("Upserting price for {ComponentName}, existing: {Exists}, version: {Version}",
    request.ComponentName, existing != null, existing?.Version);
```

---

### 2. Missing Resilience Policies for HTTP Calls (Rule 4.3)

Rule 4.3 mandates: *"Use HTTP/gRPC sparingly, wrapped in **Polly Policies**."*

**Affected files:**
- [MaterialsClient.cs](file:///d:/Projetos/poc_feature/src/PoC.Costing/Infrastructure/ExternalServices/MaterialsClient.cs) — Raw `HttpClient` with no retry, circuit breaker, or timeout policies.
- [PopulatorWorkerFunction.cs](file:///d:/Projetos/poc_feature/src/PoC.Populator/Functions/PopulatorWorkerFunction.cs) — `static HttpClient` used directly.
- [PopulationStrategies.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/PopulationStrategies.cs#L109) — Raw `GetFromJsonAsync` with no resilience.

**Fix:** Add `Microsoft.Extensions.Http.Resilience` and configure Polly policies in DI:

```csharp
builder.Services.AddHttpClient<IMaterialsClient, MaterialsClient>(client =>
{
    client.BaseAddress = new Uri(materialsUrl);
})
.AddStandardResilienceHandler(); // Adds retry + circuit breaker + timeout
```

---

### 3. Noisy Happy-Path Logging (Rule 3.1 — No Noise)

Logging every successful `GET` request at `Information` level creates massive log volume in production. This contradicts Rule 3.1: *"Comments/logs that add no value are forbidden."*

**Offending lines:**
| File | Line | Log Message |
| :--- | :--- | :--- |
| [MaterialsEndpoints.cs](file:///d:/Projetos/poc_feature/src/PoC.Materials/API/Endpoints/MaterialsEndpoints.cs#L49) | 49 | `LogInformation("[MaterialQuery] Listing materials...")` |
| [MaterialsEndpoints.cs](file:///d:/Projetos/poc_feature/src/PoC.Materials/API/Endpoints/MaterialsEndpoints.cs#L66) | 66 | `LogInformation("[MaterialQuery] Counting materials")` |
| [MaterialsEndpoints.cs](file:///d:/Projetos/poc_feature/src/PoC.Materials/API/Endpoints/MaterialsEndpoints.cs#L79) | 79 | `LogInformation("[MaterialQuery] Getting material {MaterialId}")` |
| [MaterialsEndpoints.cs](file:///d:/Projetos/poc_feature/src/PoC.Materials/API/Endpoints/MaterialsEndpoints.cs#L126) | 126 | `LogInformation("[MaterialDeletion] Deletion requested...")` |

**Fix:** Downgrade read-path logs to `Debug`. Keep `Information` only for **write operations** that change state, and `Warning`/`Error` for failures. The Serilog `RequestLogging` middleware (`UsePoCDefaults`) already handles request-level logging.

---

### 4. Lambda Functions Bypass the Observability Stack

[MaterialIngestionFunction.cs](file:///d:/Projetos/poc_feature/src/PoC.Materials/Functions/MaterialIngestionFunction.cs) and [PopulatorWorkerFunction.cs](file:///d:/Projetos/poc_feature/src/PoC.Populator/Functions/PopulatorWorkerFunction.cs) build their own DI containers and use `ILambdaContext.Logger` with **string interpolation** instead of structured logging. They completely bypass `AddPoCObservability`.

**Issues:**
1. **No structured JSON logging** — Uses `$"..."` string interpolation with `context.Logger`, producing unstructured plaintext.
2. **No trace/span correlation** — OpenTelemetry enrichment is absent.
3. **No metrics emission** — `app.startup_duration_ms` is not tracked.

**Fix:** Refactor Lambda Functions to use the same `AddPoCObservability` pipeline, or at minimum use `ILogger<T>` resolved from a properly configured DI container:

```csharp
// ❌ Before
context.Logger.LogInformation($"[MaterialIngestion] Processing {sqsEvent.Records.Count} SQS messages");

// ✅ After (structured logging with ILogger)
_logger.LogInformation("Processing {RecordCount} SQS messages", sqsEvent.Records.Count);
```

---

### 5. Static Mutable `HttpClient` in `PopulatorWorkerFunction` (Thread Safety)

[PopulatorWorkerFunction.cs:17](file:///d:/Projetos/poc_feature/src/PoC.Populator/Functions/PopulatorWorkerFunction.cs#L17): `private static readonly HttpClient _httpClient = new HttpClient();`

The `BaseAddress` is set conditionally on line 45 (`if (_httpClient.BaseAddress == null)`), which is **not thread-safe** in concurrent Lambda invocations. Multiple threads could race on the property assignment.

**Fix:** Use `IHttpClientFactory` or at least make the `BaseAddress` assignment atomic in a static constructor.

---

### 6. Inconsistent JSON Serialization Strategy

| Service | JSON Policy |
| :--- | :--- |
| PoC.Materials | `SnakeCaseLower` |
| PoC.Costing | `CamelCase` |
| PoC.Populator | `CamelCase` |
| PoC.Shared Models | `[JsonPropertyName("snake_case")]` attributes |

The shared models in `PoC.Shared` use explicit `[JsonPropertyName("snake_case")]` attributes, but `PoC.Costing` configures `CamelCase` as the default policy. This creates **serialization conflicts** — the `[JsonPropertyName]` attributes override the policy, but any new property without the attribute will serialize as `camelCase` in Costing but `snake_case` in Materials.

**Fix:** Standardize on **one** naming convention across all services. Given the existing `[JsonPropertyName]` attributes use `snake_case`, align all services to `SnakeCaseLower`.

---

### 7. Custom `ProblemDetails` Reimplementation

[ProblemDetails.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/Http/ProblemDetails.cs) reimplements `Microsoft.AspNetCore.Mvc.ProblemDetails`, which is already provided by ASP.NET Core 8. This creates maintenance overhead and risks divergence from the framework's RFC 7807 implementation.

**Fix:** Remove `PoC.Shared.Http.ProblemDetails` and `ValidationProblemDetails`. Use the built-in `Microsoft.AspNetCore.Http.Results.Problem()` (which the endpoints already do). Update `ValidationExtensions.ToProblemDetails()` to return the built-in type.

---

### 8. DI Registration Inconsistencies

| Service | AWS Client Setup | Pattern |
| :--- | :--- | :--- |
| Materials | Reads `AWS:ServiceURL` from `IConfiguration`, fallback to env vars | ✅ Configuration-aware |
| Costing | **Only** reads `AWS_ENDPOINT_URL` env var, hardcoded fallback to `localhost:4566` | ❌ Ignores configuration |
| Populator | **Only** reads `AWS_ENDPOINT_URL` env var, hardcoded fallback to `localhost:4566` | ❌ Ignores configuration |

**Fix:** Standardize all services to use `IConfiguration` first, with env var fallback. Extract a shared `AddAwsServices` extension method in `PoC.Shared.Infrastructure`.

---

## 💡 Modernization Opportunities (C# 12 / .NET 8)

Suggestions that improve code clarity and leverage modern language features.

---

### M1. Convert Mutable Classes to Records (Rule 5.2)

Rule 5.2: *"Use **Records** for DTOs, Commands, and Events."*

The following types should be `record` or `sealed record` instead of mutable `class`:

| File | Types | Current | Should Be |
| :--- | :--- | :--- | :--- |
| [Models.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/Models.cs) | `MaterialFormulation`, `Density`, `FormulationComponent` | `class` | `record` (Note: `MaterialFormulation` extends `BaseEntity`, may need refactoring) |
| [PopulationModels.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/PopulationModels.cs) | `PopulationRequest`, `PopulationJob` | `class` | `sealed record` |
| [Events.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/Events.cs) | `MaterialCreatedEvent` | `class` | `sealed record` |
| [CostingEvents.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/CostingEvents.cs) | `PriceUpdatedEvent` | `class` | `sealed record` |
| [ProblemDetails.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/Http/ProblemDetails.cs) | `ProblemDetails`, `ValidationProblemDetails` | `class` | Remove entirely (see ⚠️7) |
| [MaterialEntity.cs](file:///d:/Projetos/poc_feature/src/PoC.Materials/Infrastructure/Persistence/MaterialEntity.cs) | `MaterialEntity`, `DensityEntity`, `FormulationComponentEntity` | `class` | Keep as `class` (DynamoDB SDK requires mutable entities) |

> [!NOTE]
> DynamoDB entities (`MaterialEntity`, `ComponentPriceEntity`) must remain mutable classes because the AWS SDK's `DynamoDBContext` requires settable properties for deserialization. This is an acceptable exception.

---

### M2. Use Primary Constructors (C# 12)

The following classes use classic constructor patterns that can be simplified with Primary Constructors:

| File | Class | Current Pattern |
| :--- | :--- | :--- |
| [MaterialsClient.cs](file:///d:/Projetos/poc_feature/src/PoC.Costing/Infrastructure/ExternalServices/MaterialsClient.cs#L11) | `MaterialsClient` | Manual field assignment from constructor |
| [MaterialIngestionFunction.cs](file:///d:/Projetos/poc_feature/src/PoC/Materials/Functions/MaterialIngestionFunction.cs#L19) | `MaterialIngestionFunction` | Multi-constructor with manual DI |
| [MaterialFormulationValidator.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/Validators/MaterialFormulationValidator.cs) | All validators | Empty parameter-less constructors (already idiomatic for FluentValidation, no change needed) |

**Example fix for `MaterialsClient`:**

```csharp
// ❌ Before
public class MaterialsClient : IMaterialsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MaterialsClient> _logger;

    public MaterialsClient(HttpClient httpClient, ILogger<MaterialsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
}

// ✅ After (C# 12 Primary Constructor)
public sealed class MaterialsClient(HttpClient httpClient, ILogger<MaterialsClient> logger) : IMaterialsClient
{
    // Use httpClient and logger directly
}
```

---

### M3. Seal All Non-Inheritable Classes

The following concrete classes lack the `sealed` modifier. Sealing improves JIT performance (devirtualization) and communicates design intent:

| File | Class |
| :--- | :--- |
| [DynamoDbCostingRepository.cs](file:///d:/Projetos/poc_feature/src/PoC.Costing/Infrastructure/Persistence/DynamoDbCostingRepository.cs#L8) | `DynamoDbCostingRepository` |
| [CostCalculator.cs](file:///d:/Projetos/poc_feature/src/PoC.Costing/Domain/Services/CostCalculator.cs#L6) | `CostCalculator` |
| [MaterialsClient.cs](file:///d:/Projetos/poc_feature/src/PoC.Costing/Infrastructure/ExternalServices/MaterialsClient.cs#L6) | `MaterialsClient` |
| [PopulatorWorkerFunction.cs](file:///d:/Projetos/poc_feature/src/PoC.Populator/Functions/PopulatorWorkerFunction.cs#L12) | `PopulatorWorkerFunction` |
| [MaterialIngestionFunction.cs](file:///d:/Projetos/poc_feature/src/PoC.Materials/Functions/MaterialIngestionFunction.cs#L10) | `MaterialIngestionFunction` |
| [Result.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/Common/Result.cs#L3) | `Result` (base class, cannot seal — but should be `abstract`) |

---

### M4. `PagedResult<T>.Count` Performance Issue

[PagedResult.cs:8](file:///d:/Projetos/poc_feature/src/PoC.Shared/Common/PagedResult.cs#L8): `public int Count => Items.Count();`

This calls `Enumerable.Count()` on `IEnumerable<T>`, which iterates the entire collection each time. Since `Items` is typically a `List<T>`, this is an O(n) call per access.

**Fix:** Either change the property to `IReadOnlyList<T>` (which has O(1) `.Count`) or use a primary constructor that captures the count:

```csharp
public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? Cursor)
{
    public int Count => Items.Count; // O(1)
}
```

---

### M5. `BaseEntity` is Empty

[BaseEntity.cs](file:///d:/Projetos/poc_feature/src/PoC.Shared/Common/BaseEntity.cs): `public abstract class BaseEntity { }`

This empty class adds no behavior or constraints. It only serves as a marker type. Consider:
1. **Removing it** if it's not used for polymorphism or constraints.
2. **Converting to a marker interface** (`IEntity`) if type constraints are needed.
3. **Adding common fields** (e.g., `CreatedAt`, `UpdatedAt`) if all entities share them.

---

### M6. XML Doc Comments Violate "No Comment" Goal

[MaterialIngestionFunction.cs:14-17](file:///d:/Projetos/poc_feature/src/PoC.Materials/Functions/MaterialIngestionFunction.cs#L14), [Program.cs:39-47](file:///d:/Projetos/poc_feature/src/PoC.Materials/Program.cs#L39) and other files contain boilerplate XML `<summary>` comments like:

```xml
/// <summary>
/// Initializes a new instance of the <see cref="MaterialIngestionFunction"/> class.
/// Default constructor for Lambda runtime.
/// </summary>
```

Rule 3.1 states: *"The 'No Comment' Goal: Code must speak for itself."* These XML comments add zero value — the constructor name already tells you it initializes a new instance. Suppressing `CS1591` via `<NoWarn>` while still writing boilerplate summaries is the worst of both worlds.

**Fix:** Remove all boilerplate XML doc comments. Keep only those explaining *why* (e.g., "Default constructor required by AWS Lambda runtime").

---

## ✅ Compliance Checklist

| Category | Status | Details |
| :--- | :--- | :--- |
| **English Language** (Rule 2) | ✅ Pass | All class names, methods, variables, comments, and error messages are in English. No violations found. |
| **Layer Dependencies** (Rule 5.1) | ❌ Fail | `PoC.Shared` (Domain Kernel) references `Bogus` and contains HTTP-calling strategies (C1, C2). `IMaterialsClient` interface in wrong layer (C6). |
| **Data Sovereignty** (Rule 4.2) | ✅ Pass | No cross-service direct DB access found. Costing calls Materials via HTTP. |
| **Async First** (Rule 4.3) | ⚠️ Partial | SNS/SQS used for events ✅, but HTTP calls lack Polly ❌. |
| **No `Scan`** (Rule 6) | ❌ Fail | `GetAllAsync` and `GetCountAsync` both use `ScanAsync` (C3, C4). |
| **Structured Logging** (Rule 7) | ❌ Fail | `Console.WriteLine` in 4 files. String interpolation in Lambda loggers. |
| **Correlation IDs** (Rule 7.2) | ⚠️ Partial | OTLP TraceId enrichment configured in `Program.cs` ✅, but Lambda Functions bypass it ❌. |
| **No PII in Logs** (Rule 7.3) | ✅ Pass | No PII logging detected. |
| **Startup Metrics** (Rule 7.1) | ⚠️ Partial | `StartupTimer` implemented in `PoC.Shared.Infrastructure` ✅, but Lambda workers don't use it ❌. |
| **Result Pattern** (Rule 3.2) | ✅ Pass | All repositories and domain services return `Result<T>`. Good adoption. |
| **FluentValidation** (Rule 3.2) | ✅ Pass | All write endpoints validate with `IValidator<T>`. |
| **RFC 7807 ProblemDetails** (Rule 3.2) | ✅ Pass | Endpoints return `Results.Problem()` for errors. |
| **Records for DTOs** (Rule 5.2) | ❌ Fail | Domain models, events, and population DTOs use mutable classes (M1). |
| **File-scoped Namespaces** (Rule 5.2) | ✅ Pass | All files use file-scoped namespaces. |
| **Optimistic Locking** (Rule 6) | ✅ Pass | `[DynamoDBVersion]` attribute used on both `MaterialEntity` and `ComponentPriceEntity`. |
| **API Versioning** (Rule 14.5) | ✅ Pass | All endpoints use `/api/v1/` prefix. |
| **Cursor Pagination** (Rule 14.6) | ✅ Pass | Implemented with Base64-encoded `LastEvaluatedKey`. |
| **HATEOAS** (Rule 14.7) | ✅ Pass | `PagedResponse<T>` with `data`, `meta`, `links`. |
| **Repository Hygiene** (Rule 12) | ❌ Fail | `/scratchpad` not in `.gitignore` (C7). |
| **`.editorconfig`** (Rule 10) | ❌ Fail | No solution-level `.editorconfig` (C8). |
| **Sync-over-Async** | ✅ Pass | No `.Result` or `.Wait()` found. Clean async/await throughout. |
| **Hardcoded Secrets** | ✅ Pass | No API keys, passwords, or tokens in source. AWS credentials use `"test"` only for LocalStack. |

---

## 📊 Summary

| Severity | Count |
| :--- | :--- |
| 🚨 Critical Blockers | **8** |
| ⚠️ Major Improvements | **8** |
| 💡 Modernization | **6** |
| ✅ Compliant Areas | **14** |

### What's Good ✅

The codebase demonstrates **strong fundamentals**. The Result Pattern adoption is excellent. FluentValidation is consistently applied. File-scoped namespaces are universal. RFC 7807 ProblemDetails, cursor-based pagination, and HATEOAS are all properly implemented. No sync-over-async anti-patterns exist, and there are no hardcoded secrets.

### What Needs Urgent Attention ❌

The most critical issues are architectural: the **Shared Kernel containing Infrastructure concerns** (Bogus, HttpClient) and the **DynamoDB `Scan` usage** which will not survive production scale. These should be the first items addressed in a sprint.

---

## 🗺️ Recommended Execution Order

1. **Sprint 1 (Architecture & Critical):** C1, C2, C6, C7, C8 — Fix layer violations and hygiene.
2. **Sprint 2 (Performance & Resilience):** C3, C4, ⚠️2 (Polly), ⚠️5 — Address DynamoDB scans and HTTP resilience.
3. **Sprint 3 (Observability):** ⚠️1, ⚠️3, ⚠️4 — Clean up logging, integrate Lambda Functions.
4. **Sprint 4 (Modernization):** ⚠️6, ⚠️7, ⚠️8, M1–M6 — Records, primary constructors, sealing.

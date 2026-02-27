Logging Architecture: Standards and Best Practices (.NET)

1. The Architectural Flow (Log Pipeline)
The architecture is based on extracting structured data from the application and sending it to the observability backend with the lowest possible computational cost.
Application: Uses the native Microsoft.Extensions.Logging.ILogger<T> API.
Output 1 (OTLP gRPC/HTTP): Logs are exported directly via the OpenTelemetry Protocol to the OTel Collector.
Output 2 (Console JSON): As a fallback and for compatibility with orchestrators (Docker/Kubernetes), logs are also printed to stdout in JSON format.
Aggregation: The OTel Collector (Sidecar/DaemonSet) receives the logs, processes them, adds infrastructure metadata, and forwards them to the final backend (e.g., Grafana Loki, Elasticsearch, Datadog).

2. Output Configuration (JSON and OTel)
To facilitate ingestion and prevent multi-line messages (e.g., StackTraces) from breaking, the JSON format is mandatory in the Console.
In Program.cs or PoC.Shared, the configuration must be:

```csharp
builder.Logging.ClearProviders();

// 1. Structured JSON Console
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false }; // Indented=true destroys performance
});

// 2. OpenTelemetry Exporter
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true; // Ensures properties are indexable
});
```

3. High Performance and Volume (Source Generators)
For high-volume microservices, memory allocations (Boxing and String Allocation) are the enemy.
The use of string interpolation ($"") inside ILogger is strictly forbidden.
For logs in critical routes (Hot Paths), the team must use the [LoggerMessage] (Compile-Time Source Generator).

🔴 Bad (Causes allocation and is not structured):
```csharp
_logger.LogInformation($"Processing order {orderId} from client {clientId}");
```

🟡 Acceptable (Structured, but still uses reflection/boxing):
```csharp
_logger.LogInformation("Processing order {OrderId} from client {ClientId}", orderId, clientId);
```

🟢 Mandatory for Hot Paths (Zero Allocation, Ultra-High Performance):
```csharp
public partial class OrderService
{
    private readonly ILogger<OrderService> _logger;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Processing order {OrderId} from client {ClientId}")]
    public partial void LogProcessingOrder(Guid orderId, string clientId);
    
    // Usage:
    // _logger.LogProcessingOrder(orderId, clientId);
}
```

4. The Log Levels Standard
The trivialization of log levels generates alert fatigue and massive storage costs. Follow this matrix:

| Level | Usage | Alert? | Retention |
| :--- | :--- | :--- | :--- |
| Trace / Debug | Microscopic details of the code flow (e.g., "Entered loop X"). Disabled in Production. | No | N/A |
| Information | Important Business events (e.g., "Order created", "Payment approved"). Indicates the "Happy Path". Do not log every method step. | No | Medium |
| Warning | Anomalous but recoverable situations. Ex: "External API failed, triggering Polly Retry", "Invalid optional parameter, using default". | No | Long |
| Error | Unhandled exceptions, interrupted business flows. The user received an HTTP 500. Ex: "Failed to write to DynamoDB". | Yes (Monitor Rate) | Long |
| Critical | The system is corrupted or offline. Ex: "OutOfMemoryException", "Invalid DB Credentials", "Disk Partition Full". | Yes (Wake up SRE/On-call) | Long |

5. Traceability and Context (Scopes & Correlation)
Isolated logs are useless in microservices. They must always have context.
TraceId & SpanId: The .AddOpenTelemetry() method automatically injects the TraceId and SpanId into every log line (if the request has an active Activity). Never create these IDs manually.
Scopes: Use BeginScope for batch operations or long flows. All log messages emitted within the using block will inherit these properties in the JSON output.

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["TenantId"] = tenantId,
    ["CorrelationId"] = correlationId
}))
{
    _logger.LogInformation("Starting processing"); // Includes TenantId
    // ... call other methods ...
    _logger.LogWarning("Partial timeout"); // Includes TenantId
}
```

6. Governance and Security
No PII (Personally Identifiable Information): Never log SSNs/CPFs, Credit Cards, Passwords, or JWT Tokens (not even at the Debug level).
PascalCase Naming: Property keys in the log must be PascalCase (e.g., {UserId} and not {user_id}). This standardizes querying in Grafana/Loki.

How to apply this to your project right now?
If you want the AI to review your current code and adapt everything to use this architecture (especially implementing the [LoggerMessage] for current logs), use this prompt in your IDE:

```plaintext
@POC_RULES.md @src/

Act as a **Principal Software Engineer**.
We are enforcing a new High-Performance Cloud-Native Logging Architecture based on the native `ILogger` and `[LoggerMessage]` source generators.

**Task:**
Review the codebase (specifically Application and API layers) and refactor the logging implementation:

1. **Remove all String Interpolation:** Scan for `_logger.Log...($"...")` and convert them immediately to structured logging or Source Generators.
2. **Implement `[LoggerMessage]`:** For frequently called classes (e.g., Repositories, Domain Services, Use Cases), create a partial class and define `[LoggerMessage]` attributes for all logging events. Replace the old `_logger.LogXXX` calls with these new highly optimized partial methods.
3. **Naming Conventions:** Ensure all structured log parameters use PascalCase (e.g., `{OrderId}`, not `{order_id}`).
4. **Scope Review:** Identify areas where a batch of operations is performed (e.g., an SQS Message Consumer) and wrap the execution in a `_logger.BeginScope` with the `MessageId` and relevant domain context.

Ensure that the project files (`.csproj`) have the necessary configurations to support the source generators.
```

## Implementation Status (2026-02-22)

**Status: COMPLETE**

### Completed Items
1.  **Source Generators:** `[LoggerMessage]` attributes are being used in:
    - **Repositories:** `DynamoDbMaterialRepository`, `DynamoDbCostingRepository`.
    - **Functions:** `PopulatorWorkerFunction`, `PriceIngestionFunction`, `MaterialIngestionFunction`.
    - **Domain Services:** `PopulationService`.
    - **API Endpoints:** `PopulatorEndpoints`, `FeatureGateExtensions`.
    - **Clients:** `MaterialsClient`.
2.  **String Interpolation:** No instances of string interpolation (`$""`) found in `_logger.Log...` calls.
3.  **Observability Enabled:** The centralized configuration `AddPoCObservability` has been enabled in `PoC.Materials`, `PoC.Costing`, and `PoC.Populator` Program files.
4.  **Refactoring:** The code structure fully supports the new logging architecture with partial classes and methods.

### Pending Actions
None. The logging architecture is fully implemented.

# Audit: OpenTelemetry Implementation in Lambdas

## 1. Overview & Current State
The project uses a **centralized observability strategy** defined in `PoC.Shared.Infrastructure`. This ensures consistency across all services.

- **Stack:** Serilog (Logs) + OpenTelemetry (Tracing/Metrics).
- **Configuration:** Shared extension method `AddPoCObservability`.
- **Lambda Support:** Explicitly supported.
    - Uses `ExportProcessorType.Simple` (synchronous export) which is correct for Lambda to ensure data is flushed before the execution environment freezes.
    - Sets a 3-second timeout for exports to prevent hanging.
- **Protocol:** Defaults to gRPC (`http://localhost:4317`), configurable via `appsettings.json`.

All three key services currently implement this standard:
- `PoC.Costing` (API)
- `PoC.Materials` (API)
- `PoC.Populator` (API & Worker)

## 2. Critical Issues Identified

### 2.1. Blocking Logs in `PoC.Populator` (Performance Killer)
**Symptoms:** The lambda logs repetitively and fails to process requests (timeouts).
**Root Cause:**
In `PopulatorWorkerFunction.cs`, there is a `foreach` loop processing items from the job. Inside this loop, **Info-level logs are written for every single item**:
```csharp
// Processing job...
// Publishing event...
```
- **Synchronous Locking:** Serilog's Console Sink (used in Lambda) writes synchronously.
- **Volume:** If a job requests 1,000 items, the Lambda attempts to write 2,000+ log lines to CloudWatch *synchronously*.
- **Consequence:** This I/O overhead likely causes the Lambda to time out before completing the batch, especially if the `BatchSize` is large.

### 2.2. Expensive Host Initialization
**Location:** `PopulatorWorkerFunction` Constructor.
```csharp
public PopulatorWorkerFunction()
{
    var builder = Host.CreateApplicationBuilder();
    // ... builds entire DI container
}
```
**Issue:**
- A new Dependency Injection (DI) container is built **every time the class is instantiated**.
- While AWS Lambda *may* reuse the class instance, any cold start or instance recycling incurs the heavy cost of building the Host.
- This increases Cold Start duration significantly and wastes memory.

### 2.3. Broken Distributed Tracing (SQS)
**Location:** `PopulatorWorkerFunction`.
**Issue:**
- The worker receives an `SQSEvent`.
- It does **not** extract the `TraceParent` header from the SQS Message Attributes.
- **Consequence:** The trace stops at the publisher. The worker starts a completely new, disconnected trace. You cannot see the full flow from "Job Request" -> "SQS" -> "Worker Execution".

## 3. Remediation Plan

### 3.1. Immediate Fixes (Performance)
1.  **Reduce Log Noise:**
    - Move loop-internal logs from `Information` to `Debug`.
    - Log only a **summary** at the end of the batch (e.g., "Processed 1000 items in 500ms").
    - **Files to Change:** `src/PoC.Populator/Functions/PopulatorWorkerFunction.cs`

2.  **Optimize Host Lifecycle:**
    - Refactor `PopulatorWorkerFunction` to use a **Static Lazy Initialization** pattern or a proper Lambda DI framework (like `Amazon.Lambda.AspNetCoreServer` or manual static container).
    - Ensure the `Host` is built only once per processes.

### 3.2. Strategic Improvements (Observability)
1.  **Implement SQS Context Propagation:**
    - Create a helper `SqsParentContextExtractor`.
    - In `FnHandler`, read the `traceparent` from `ev.Records[].MessageAttributes`.
    - Start a new `Activity` using that parent context.
    - **Benefit:** End-to-end tracing visibility.

2.  **Review OTLP Networking:**
    - Ensure the `Otel:Endpoint` environment variable in the Lambda configuration points to a reachable collector (e.g., `http://otel-collector:4317` if in a VPC, or a public endpoint).
    - If running in a VPC without NAT/Endpoints, the export will fail (and timeout after 3s per batch, further slowing down processing).

## 4. Proposed RoadMap
1.  **Phase 1 (Fix):** Patch `PopulatorWorkerFunction` to remove logging inside loops.
2.  **Phase 2 (Refactor):** Lift `Host.CreateApplicationBuilder` out of the constructor.
3.  **Phase 3 (Enhance):** Add Trace Context extraction for SQS.

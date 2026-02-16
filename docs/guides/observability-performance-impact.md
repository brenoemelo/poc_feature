# Observability Performance Impact & Warm-up Analysis

## 🎯 Objective
Measure the **overhead** introduced by OpenTelemetry (OTLP) on **AWS Lambda Cold Starts** (Warm-up) and **Memory Consumption**.

Adding observability libraries (Serilog + OTel) increases the deployment package size and initialization time. This guide defines a **standardized, automated process** to quantify this impact and decide on optimizations (e.g., AOT, Trimming).

---

## 📊 Key Metrics to Watch

| Metric (CloudWatch) | Description | OTel Impact Risk | Target (PoC) |
|---|---|---|---|
| **Init Duration** | The "Cold Start" time. Time to load the runtime + static constructors + DI container. | **High**, due to JIT compilation of OTel libraries and DI registration. | < 600ms (Tier 1) |
| **Duration** | Execution time of the handler method. | **Low**, OTel is async (BatchProcessor). | < 10ms overhead |
| **Max Memory Used** | Peak memory during execution. | **Medium**, buffering spans/logs in memory before export. | < 128MB overhead |
| **Package Size** | Size of the zipped artifact. | **Medium**, affects `Init Duration`. | < 50MB (unzipped) |

---

## 🤖 Automated Measurement Plan

We use **CloudWatch Logs Insights** and a **Load Test** script to compare "Baseline" (No OTel) vs "Implementation" (With OTel).

### 1. The Experiment Script (PowerShell/Bash)

Create a script `scripts/measure-cold-start.ps1` that:

1.  **Deploy Baseline:** Deploy the Lambda with `OTEL_SDK_DISABLED=true` (or remove the `AddPoCObservability` call via pre-processor directive `#if !NO_OTEL`).
2.  **Force Cold Starts:**
    *   Update the Lambda Configuration (e.g., change a dummy Env Var) to force a new execution environment.
    *   Invoke the Lambda 10 times sequentially.
3.  **Deploy OTel:** Deploy with OTel enabled.
4.  **Force Cold Starts:** Repeat the update & invoke process.
5.  **Analyze:** Query CloudWatch Logs.

### 2. CloudWatch Insights Query

Use this query to extract precise metrics from `REPORT` log lines:

```sql
filter @type = "REPORT"
| parse @message /Init Duration: (?<init_duration>[0-9.]+) ms/
| parse @message /Duration: (?<duration>[0-9.]+) ms/
| parse @message /Max Memory Used: (?<max_memory>[0-9.]+) MB/
| stats 
    avg(init_duration) as AvgColdStart, 
    max(init_duration) as MaxColdStart,
    avg(max_memory) as AvgMemory,
    max(max_memory) as MaxMemory
  by bin(1h)
```

---

## 📉 Mitigating Cold Start Impact

If `Init Duration` increases significantly (> 300ms added), apply these optimizations in `PoC.Shared.Infrastructure`:

### 1. Optimization Levels

| Level | Strategy | Pros | Cons |
|---|---|---|---|
| **L1 (Quick)** | **Lazy Initialization**: Defer `AddPoCObservability` until the first request? (Hard with DI). | Low effort. | Moves cost to first request (still latency). |
| **L2 (Standard)** | **Trim Unused instrumentation**: Remove `OpenTelemetry.Instrumentation.AWS` or `Http` if not used. | Reduces Jit/Size. | Less visibility. |
| **L3 (Advanced)** | **ReadyToRun (R2R)**: Pre-compile assemblies in CI/CD. | Faster startup. | Larger package size. |
| **L4 (Modern)** | **NativeAOT**: Compile to native code (No JIT). | **Zero cold start overhead**. | Requires significant code changes (JSON serialization, DI). |

### 2. Configure Memory-Conscious Processor
For Lambda, standard OTel batching (512 items/5secs) is dangerous because the environment freezes.
**Recommendation:** Use the `SimpleSpanProcessor` or aggressive Batching for Lambda.

```csharp
// In ServiceCollectionExtensions.cs
if (isLambda)
{
    // Exports immediately to avoid data loss on Freeze
    builder.AddOtlpExporter(o => o.ExportProcessorType = ExportProcessorType.Simple);
}
```

---

## 🧪 Testing Checklist

Apply this checklist to every new project inheriting this stack:

- [ ] **Baseline Established:** Record `Init Duration` average of current prod.
- [ ] **OTel Integration:** Add `ServiceCollectionExtensions`.
- [ ] **Sanity Check:** Max Memory < Lambda Limit (usually 128MB/256MB).
- [ ] **Load Test:** Run 50 concurrent cold starts.
- [ ] **Decision:** If overhead < 15%, Approave. If > 15%, investigate L2/L3 optimizations.

---

## 📝 Example Report Template

| Metric | Baseline (No OTel) | With OTel | Diff | Status |
|---|---|---|---|---|
| **Avg Cold Start** | 250 ms | 550 ms | +300 ms | ⚠️ Warning |
| **Warm Execution** | 15 ms | 18 ms | +3 ms | ✅ OK |
| **Max Memory** | 65 MB | 85 MB | +20 MB | ✅ OK |
| **Artifact Size** | 12 MB | 18 MB | +6 MB | ✅ OK |

*Action Item: Enable ReadyToRun in `.csproj` to reduce Cold Start.*
```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

# 01 - Observability Strategy Overview

## Context
The goal is to implement full observability (Logs, Metrics, Traces) for the .NET 8 Microservices running on AWS Lambda. The current environment is a LocalStack-based PoC, but the solution must be production-ready for AWS.

## Architecture Decisions

### 1. Hybrid Instrumentation (ASP.NET Core + AWS Lambda)
We will maintain the current **ASP.NET Core Hosted** model (using `Amazon.Lambda.AspNetCoreServer.Hosting`) but enhance it with **AWS Lambda Instrumentation**.

*   **Why?** The current project uses `Controllers` and `Minimal APIs`, which requires the ASP.NET Core host. However, without specific Lambda instrumentation, we lose:
    *   **X-Ray Context Propagation**: Traces from API Gateway/SQS won't connect to the application traces.
    *   **Reliable Flushing**: In a frozen Lambda environment, background threads (like the OTel Batch Processor) may be paused before sending data. We need a mechanism to flush data *before* the Lambda freezes.

### 2. Infrastructure: The "Sidecar" Simulation
In production (AWS), we will use the **ADOT Lambda Layer** (AWS Distro for OpenTelemetry) which runs a Collector instance as an extension.

In LocalStack (Docker), we simulate this by running a standalone **OpenTelemetry Collector** container.
*   **Application (Lambda)** sends OTLP data to -> **Collector Container** (port 4317/4318).
*   **Collector** processes and exports to -> **LGTM Stack** (Loki, Grafana, Tempo, Mimir/Prometheus).

### 3. Protocol: OTLP over HTTP
We will standardise on **OTLP/HTTP (Protobuf)**.
*   **Why?** gRPC can be tricky with load balancers and some Lambda extensions. HTTP is robust and universally supported.

## The Gap
Currently, the application:
1.  Has OTel for ASP.NET Core (`OpenTelemetry.Instrumentation.AspNetCore`).
2.  Has OTel for AWS SDK (`OpenTelemetry.Instrumentation.AWS`).
3.  **MISSING**: `OpenTelemetry.Instrumentation.AWSLambda` which provides the lifecycle hooks for serverless environments.

## Next Steps
Proceed to [02-Implementation Steps](./02-implementation-steps.md) to apply the fixes.

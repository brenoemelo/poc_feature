# ADR 001: Microservices Technology Stack

## Status
Accepted

## Context
We are building a **Material Formulation System** that requires:
- High scalability (potential for millions of formulations).
- Cost-effective scaling (scale-to-zero).
- Strict data isolation between domains (Materials vs Costing).
- High observability for distributed transactions.

## Decision
We chose the following stack:

### 1. Compute: AWS Lambda (.NET 8)
- **Why .NET 8?** High performance, strong typing, and excellent Native AOT support (crucial for Lambda Cold Starts).
- **Why Lambda?** Serverless model aligns with our sporadic workload patterns and reduces operational overhead.

### 2. Storage: Amazon DynamoDB
- **Why NoSQL?** The schema (Formulations) is flexible and hierarchical.
- **Why DynamoDB?** Single-digit millisecond latency at any scale. Supports the **Single Table Design** pattern for efficient relationship modeling.

### 3. Observability: OpenTelemetry (OTLP)
- **Why not CloudWatch X-Ray?** Vendor lock-in.
- **Why OTel?** Industry standard. Allows us to switch backends (e.g., to Datadog or Grafana Tempo) by changing configuration, not code.

### 4. Feature Flags: OpenFeature + Unleash
- **Why?** Decouples deployment from release. Vendor-agnostic standard (OpenFeature). Unleash provides a rich UI and enterprise features.

## Consequences
- **Positive:** extremely low idle cost, high performance, standardized observability.
- **Negative:** Cold starts in Lambda need management (AOT/Provisioned Concurrency). DynamoDB modeling requires a learning curve compared to SQL.

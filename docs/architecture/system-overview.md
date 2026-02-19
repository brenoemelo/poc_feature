# System Architecture Overview

## 1. High-Level Design

The Material Formulation System is a **Cloud-Native Microservices Architecture** running on **AWS Lambda** (serverless compute) and **DynamoDB** (NoSQL storage). It is designed for high scalability, event-driven communication (SNS/SQS), and strict observability.

### Request Flow Diagram

```mermaid
graph LR
    Client([Client Application]) -->|HTTPS| APIGW[API Gateway]
    APIGW -->|Route| Lambda[AWS Lambda<br/>(.NET 8)]
    
    subgraph "Microservice (e.g., PoC.Materials)"
        Lambda -->|Json| Endpoint[Minimal API Endpoint]
        Endpoint -->|DTO| Domain[Domain Logic]
        Domain -->|Entity| Infra[Infrastructure Layer]
        
        subgraph "Internal Components"
            Filters[Filters<br/>(Feature Flags)]
            Pipeline[Behaviors<br/>(Validation/Logging)]
        end
    end

    Infra -->|Read/Write| DDB[(Amazon DynamoDB)]
    Infra -->|Publish| SNS[Amazon SNS]
    
    subgraph "Sidecars (Docker/ECS)"
        OTEL[OTel Collector]
        UNLEASH[Unleash]
    end

    Lambda -.->|gRPC/HTTP| OTEL
    Lambda -.->|HTTP| UNLEASH
```

## 2. Core Architectural Patterns

### 2.1. Clean Architecture
Each microservice follows the **Clean Architecture** principles to separate concerns and ensure testability.

| Layer | Responsibility | Components |
|---|---|---|
| **API (Presentation)** | Entry point, HTTP protocols, Serialization. | `Program.cs`, `Endpoints/`, `Filters/` |
| **Application** | Use cases, specific business logic. | *Merged with Domain in this PoC for simplicity* |
| **Domain (Core)** | Enterprise business rules, Entities, Aggregates. | `Models/`, `Interfaces/`, `ValueObjects/` |
| **Infrastructure** | External concerns (DB, Bus, File System). | `Repositories/`, `Services/` |

> **Rule:** Dependencies point **inwards**. The Domain/Core knows nothing about the Database or API.

### 2.2. Event-Driven Communication
Services are decoupled and communicate asynchronously via **Integration Events**.

- **SNS (Simple Notification Service):** Used for **Pub/Sub**. A service publishes an event (e.g., `MaterialCreated`) to a Topic.
- **SQS (Simple Queue Service):** Used for **Queueing**. Services subscribe to Topics via SQS Queues to process events reliably.

**Example Flow:**
1. `PoC.Materials` publishes `MaterialCreated` to `sns-materials-events`.
2. `PoC.Costing` (subscribed via `sqs-costing-material-updates`) receives the message.
3. `PoC.Costing` Lambda wakes up and calculates the new price.

### 2.3. Shared Kernel (`PoC.Shared`)
To avoid code duplication in cross-cutting concerns, we use a Shared Kernel library.

- **Contains:** Result Pattern, Base Entities (`IEntity`), Common DTOs, Observability setup, Feature Flag wrappers.
- **Does NOT Contain:** Business logic specific to one domain (e.g., "Pricing Rules").

## 3. Observability & Telemetry

The system is fully instrumented using the **OpenTelemetry (OTel)** standard.

- **Tracing:** W3C Trace Context is propagated across API Gateway, Lambda, SNS, and SQS.
- **Metrics:** Business and technical metrics (e.g., `orders_processed`, `execution_time_ms`) are emitted to the Collector.
- **Logs:** Structured logs (Serilog) are enriched with `TraceId` and `SpanId` for correlation.

**Data Flow:**
App -> OTel SDK -> OTel Collector (Sidecar) -> Backends (Tempo/Prometheus/Loki)

## 4. Feature Management

We use **OpenFeature** to decouple deployment from release.

- **Provider:** Unleash (container).
- **Mechanism:** Flags are evaluated **in-process** (e.g., inside the Lambda/Container) using cached rules polled from the provider.
- **Fail-Safe:** If the provider is unreachable, flags default to `false` (Disabled).

## 5. Persistence Strategy (Single Table Design)

While each service "owns" its data, we prefer **DynamoDB Single Table Design** principles where appropriate for performance, though currently, services may use separate tables for strict isolation (**Data Sovereignty**).

- **PK/SK Pattern:** Primary Key (`PK`) and Sort Key (`SK`) are used to model relationships (e.g., `PK=MAT#123`, `SK=VER#1`).
- **Optimistic Locking:** Setup via `VersionNumber` to prevent lost updates.

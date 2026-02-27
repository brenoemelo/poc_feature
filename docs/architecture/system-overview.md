# System Architecture Overview

## 1. High-Level Design

The Material Formulation System is a **Cloud-Native Microservices Architecture** running on **AWS Lambda** (serverless compute) and **DynamoDB** (NoSQL storage). It is designed for high scalability, event-driven communication (SNS/SQS), and strict observability.

### Request Flow Diagram

```mermaid
graph LR
    Client([Client Application]) -->|HTTPS| APIGW[API Gateway]
    APIGW -->|Route| Lambda[AWS Lambda<br/>(.NET 10)]
    
    subgraph "Microservice (e.g., PoC.Materials)"
        Lambda -->|Json| Endpoint[Minimal API Endpoint]
        Endpoint -->|DTO| App[Application Layer]
        App -->|Entity| Domain[Domain Layer]
        App -->|Interface| Infra[Infrastructure Layer]
        
        subgraph "Internal Components"
            Filters[Filters<br/>(Feature Flags)]
            Pipeline[Behaviors<br/>(Validation/Logging)]
        end
    end
    
    Infra -->|Read/Write| DDB[(Amazon DynamoDB)]
    Infra -->|Publish| SNS[Amazon SNS]
    
    subgraph "Sidecars (Docker)"
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
| **Application** | Use cases, Validators, DTOs. | `UseCases/`, `Validators/` |
| **Domain (Core)** | Enterprise business rules, Entities, Aggregates. | `Models/`, `Interfaces/`, `ValueObjects/` |
| **Infrastructure** | External concerns (DB, Bus). | `Repositories/`, `Services/` |

> **Rule:** Dependencies point **inwards**. The Domain/Core knows nothing about the Database or API.

### 2.2. Event-Driven Communication
Services are decoupled and communicate asynchronously via **Integration Events**.

- **SNS (Simple Notification Service):** Used for **Pub/Sub**. A service publishes an event (e.g., `MaterialCreated`) to a Topic.
- **SQS (Simple Queue Service):** Used for **Queueing**. Services subscribe to Topics via SQS Queues to process events reliably.

**Example Flow:**
1. `PoC.Materials` publishes `MaterialCreated` to `sns-materials-events`.
2. `PoC.Costing` (subscribed via `sqs-costing-material-updates`) receives the message.
3. `PoC.Costing` Lambda wakes up and calculates the new price.

### 2.3. Specialized Shared Libraries
To avoid the "Kitchen Sink" anti-pattern, we use focused libraries:

- **`PoC.Shared`:** Lightweight Kernel. Contains Result Pattern (`Result<T>`), Base Entities, Common DTOs.
- **`PoC.Observability`:** **Rule:** All OTel and logging configuration resides here. Uses Native ILogger (No Serilog).
- **`PoC.FeatureFlags`:** OpenFeature implementation with Unleash provider and local-safe fallback.

## 3. Observability & Telemetry

The system is fully instrumented using the **OpenTelemetry (OTel)** standard.

- **Tracing:** W3C Trace Context is propagated across API Gateway, Lambda, SNS, and SQS.
- **Metrics:** Business and technical metrics (e.g., `business_costing_value`, `http_server_duration`) are emitted to the Collector.
- **Logs:** Native structured logs enriched with `TraceId` and `SpanId` for correlation.

**Data Flow:**
App -> OTel SDK -> OTel Collector (Sidecar) -> Backends (Tempo/Prometheus/Loki)

## 4. Feature Management

We use **OpenFeature** to decouple deployment from release.

- **Provider:** Unleash (container).
- **Mechanism:** Flags are evaluated **in-process** using cached rules polled from the provider.
- **Fail-Safe:** Supports a `FakeUnleash` provider for local environments. Defaults to `false` if unreachable.

## 5. Persistence Strategy

- **Data Sovereignty:** Each service owns its tables.
- **PK/SK Pattern:** Primary Key (`PK`) and Sort Key (`SK`) are used to model relationships efficiently.
- **Optimistic Locking:** Setup via `VersionNumber` to prevent lost updates.

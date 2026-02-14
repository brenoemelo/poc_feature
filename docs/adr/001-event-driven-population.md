# ADR 001: Event-Driven Population Architecture

## Status
Accepted

## Context
The goal is to create a robust population mechanism that adheres to Microservices principles, specifically **Data Sovereignty** (Rule 4.2). The initial implementation allowed `PoC.Populator` to write directly to `PoC.Lambda`'s DynamoDB table, creating tight coupling and violating the "Shared Nothing" principle.

## Decision
We will decouple the generation of data from the ingestion/persistence of data using an asynchronous event-driven pattern.

1.  **PoC.Populator (Producer):** Responsible for executing population jobs and generating synthetic data. Instead of writing correct DB records, it publishes `MaterialCreatedEvent` to an SNS Topic `material-events`.
2.  **PoC.Lambda (Consumer):** Responsible for the Material Domain. It subscribes to `material-events` via an SQS Queue `material-ingestion-queue` and persists the data to its own DynamoDB table.

## Consequences
### Positive
*   **Decoupling:** Services can be deployed independently. `PoC.Populator` doesn't need to know `PoC.Lambda`'s table schema, only the Event contract.
*   **Scalability:** SNS/SQS allows for buffering and scaling consumers independently.
*   **Compliance:** Meets Rule 4.2 (Data Sovereignty) and Rule 4.3 (Async First).

### Negative
*   **Complexity:** Managing infrastructure (SNS, SQS, Subscriptions) is more complex than a direct DB write.
*   **Latency:** Data is eventually consistent. The API returns `202 Accepted` but the data might appear milliseconds later.

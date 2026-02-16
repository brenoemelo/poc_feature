# AI Context Map (Architecture & Codebase)

**Role:** This file serves as the **Primary Context** for AI Assistants (Trae, Cursor, Copilot).
**Goal:** Provide high-level understanding of the system, constraints, and locations of key information.

## 1. System Identity
- **Name:** Material Formulation System (PoC)
- **Tech Stack:** .NET 8 (Native AOT capable), AWS Lambda, DynamoDB, APIs Gateway.
- **Architecture:** Event-Driven Microservices (Clean Architecture).
- **Observability:** OpenTelemetry (OTLP) + Serilog -> Collector -> Tempo/Prometheus.
- **Features:** Managed via OpenFeature + GoFeatureFlag (Sidecar).

## 2. Key Directories
- `src/PoC.*`: Microservices (Materials, Costing, Populator).
- `src/PoC.Shared`: Shared Kernel (Results, DTOs, Infra filters).
- `docs/architecture`: System Design & Diagrams.
- `docs/guides`: Coding Standards, How-To's.
- `scripts/`: Operational scripts (PowerShell).

## 3. Critical Constraints (The "Rules")
1.  **Strict Layering:** Domain calls nothing. API calls Application/Infra. Infra calls External.
2.  **No Exceptions:** Use `Result<T>` pattern for control flow.
3.  **No Offset Pagination:** Always use Cursor-based (Base64 token).
4.  **Observability:** All public methods must be instrumented. Logs must be structured.
5.  **Feature Flags:** Use `[WithFeatureGate]` (Declarative) over `if/else`.

## 4. Request Lifecycle
`Client` -> `APIGW` -> `Lambda` -> `Filter (Auth/Flag)` -> `Endpoint` -> `Validator` -> `Repository (DynamoDB)` -> `Response (Envelope)`

## 5. Development Workflow
1.  **Start Infra:** `docker compose up -d` (LocalStack + OTel + Flags).
2.  **Deploy:** `./deployment/localstack/deploy-all.ps1`
3.  **Test:** `./scripts/tests/test_all_apis.ps1`
4.  **Debug:** Use `traceId` in Grafana Tempo.

## 6. Documentation Index
- **Architecture:** [docs/architecture/system-overview.md](docs/architecture/system-overview.md)
- **API Standards:** [docs/guides/api-standards.md](docs/guides/api-standards.md)
- **Troubleshooting:** [docs/operations/troubleshooting.md](docs/operations/troubleshooting.md)

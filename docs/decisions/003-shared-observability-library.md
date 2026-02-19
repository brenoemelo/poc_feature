# ADR 003: Shared Observability Infrastructure Library

## Status
Accepted

## Context
Each microservice in the PoC project originally implemented its own observability logic (Logging configuration, exception handling, and basic logging). This led to:
1.  **Code Duplication:** Substantial boilerplate in `Program.cs` for every service.
2.  **Inconsistency:** Variations in logging formats and metrics implementation.
3.  **Vendor Lock-in Risk:** Hardcoding specifics for logging backends within services.
4.  **Lack of Correlation:** Difficulty linking logs and traces across service boundaries.

## Decision
We decided to extract all observability, logging, and metrics logic into a dedicated shared library: `PoC.Shared.Infrastructure`.

### High-Level Design:
- **Common Extension Method:** `AddPoCObservability(serviceName, version)` to configure the entire stack in one line.
- **Protocol-First Approach:** Strictly use OTLP (OpenTelemetry Protocol) for traces, metrics, and logs.
- **Log Correlation:** Automatic injection of `TraceId` and `SpanId` into every log message.
- **Shared Middleware:** `UsePoCDefaults()` to handle standardized RFC 7807 exception responses and request logging.
- **Configuration-Driven:** Use `appsettings.json` and environment variables to switch between backends (e.g., Grafana, Datadog) without code changes.

## Consequences

### Positive:
- **Zero Boilerplate:** Reducing microservice `Program.cs` size by ~40-60%.
- **Vendor Agnostic:** Easy to migrate between observability platforms by changing only a few lines of configuration.
- **Improved Troubleshooting:** Unified log-to-trace correlation makes debugging distributed transactions much faster.
- **Developer Experience:** New services get production-grade monitoring out of the box.

### Negative:
- **Infrastructure Dependency:** All services now depend on a shared infrastructure project, slightly increasing the build dependency graph (but still separate from the core Domain Kernel).
- **Package Management:** Updating OpenTelemetry versions now requires a release of the shared library.
- **AWS SDK Complexity:** Upgrading OTel sometimes forces upgrades of AWS SDK packages (resolved by standardizing versions).

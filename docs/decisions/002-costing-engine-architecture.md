# ADR 002: Costing Engine Architecture

## Status
Accepted

## Context
The business requires a **Costing Engine** to transform material formulations into financial data. This includes:
1. Managing a catalog of component prices
2. Calculating the total cost of a formulation based on component percentages
3. Providing margin analysis for pricing decisions

The implementation must respect **Data Sovereignty** (Rule 4.2), meaning the Costing service cannot directly access other services' databases.

## Decision
We will implement `PoC.Costing` as an independent microservice with its own DynamoDB table (`costing-prices-table`).

### Architecture

```
┌─────────────────┐
│  API Gateway    │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼──────┐  │
│ PUT      │  │
│ /prices  │  │
│          │  │
│ Price    │  │
│ Mgmt     │  │
│ Function │  │
└────┬─────┘  │
     │        │
     ▼        │
┌──────────────────┐
│ costing-prices-  │
│ table (DynamoDB) │
│                  │
│ PK: ComponentName│
└──────────────────┘
         ▲
         │
    ┌────┴─────┐
    │ POST     │
    │ /calc... │
    │          │
    │ Cost     │
    │ Calc     │
    │ Function │
    └──────────┘
```

### Key Design Decisions

1. **Separate DynamoDB Table:** `costing-prices-table` stores component prices with `ComponentName` as the partition key, enabling efficient `GetItem` lookups (no `Scan`).

2. **Formulation Data in Request Body:** The `/calculate-cost` endpoint receives the formulation data directly in the request payload, not by querying `poc-table`. This ensures **Data Sovereignty**.

3. **FluentValidation:** Business rules are enforced using FluentValidation:
   - Formulation percentages must sum to 100%
   - All components must have prices in the catalog
   - Currency must be consistent across all components

4. **RFC 7807 ProblemDetails:** All errors return standardized ProblemDetails responses per Rule 3.3.

5. **Margin Calculation:** Optional margin analysis calculates:
   - `selling_price = total_cost / (1 - margin/100)`
   - `gross_profit = selling_price - total_cost`

## Consequences

### Positive
*   **Independence:** Costing service can be deployed, scaled, and maintained independently.
*   **Performance:** Direct key lookups on `ComponentName` avoid expensive `Scan` operations.
*   **Validation:** FluentValidation provides clear, testable business rules.
*   **Compliance:** Meets Rule 4.2 (Data Sovereignty), Rule 3.2 (Validation), and Rule 3.3 (Error Handling).

### Negative
*   **Data Duplication:** Formulation data must be sent in the request body, potentially duplicating data from `poc-table`.
*   **Currency Management:** The system currently requires all components to use the same currency. Multi-currency support would require exchange rate logic.

## Alternatives Considered

1. **Read from `poc-table`:** Rejected because it violates Data Sovereignty.
2. **Event-Driven Sync:** Could subscribe to `MaterialCreatedEvent` to pre-calculate costs. Deferred for future iteration.

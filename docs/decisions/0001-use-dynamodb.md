# ADR 0001: Use DynamoDB as Primary Database

## Status

Accepted

## Context

The Material Formulation System requires a database solution that can:

- Handle flexible schemas for material properties
- Scale horizontally for high-throughput operations
- Support fast key-value lookups for material retrieval
- Integrate seamlessly with AWS Lambda functions
- Minimize operational overhead in a serverless architecture

## Decision

We will use **Amazon DynamoDB** as the primary database for all microservices.

### Table Design

Each microservice owns its own table(s):

- **poc-table** - Material formulations (owned by PoC.Lambda)
- **costing-prices-table** - Component prices (owned by PoC.Costing)

### Key Schema

- **poc-table**: Partition Key = `Id` (String)
- **costing-prices-table**: Partition Key = `ComponentName` (String)

## Consequences

### Positive

- **Serverless-Native**: DynamoDB integrates natively with Lambda, reducing cold start times
- **Scalability**: Automatic scaling handles variable workloads without manual intervention
- **Performance**: Single-digit millisecond latency for key-value operations
- **Schema Flexibility**: JSON-like documents support evolving material property schemas
- **Cost-Effective**: Pay-per-request pricing aligns with PoC usage patterns

### Negative

- **Query Limitations**: Complex queries require careful index design or secondary indexes
- **Learning Curve**: NoSQL modeling differs from traditional relational databases
- **Eventual Consistency**: Default read consistency is eventual (can be overridden with strongly consistent reads)
- **No Joins**: Denormalization required for related data

## Implementation Notes

- Use `Query` operations with partition keys; avoid `Scan` operations
- Implement optimistic locking with version numbers for concurrent updates
- Use `BatchWriteItem` for bulk inserts (max 25 items per batch)
- Configure LocalStack for local development and testing

## Alternatives Considered

1. **PostgreSQL (RDS)**: Rejected due to operational overhead and cold start latency with Lambda
2. **MongoDB (DocumentDB)**: Rejected due to higher cost and complexity for this PoC
3. **S3 + Athena**: Rejected as it's optimized for analytics, not transactional workloads

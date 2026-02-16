# Operational Troubleshooting Guide

This guide covers **Health Checks** to verify deployment status and **Runbooks** for common failure scenarios.

## Part 1: Deployment Health Checks

Use these steps to verify if the LocalStack environment is healthy.

### 1. API Gateway & Functions
```bash
# List APIs
docker exec poc_feature-localstack-1 awslocal apigateway get-rest-apis

# List Functions
docker exec poc_feature-localstack-1 awslocal lambda list-functions
```

### 2. Verify Connectivity
```bash
# Test the Materials endpoint (replace 'material-api' if needed)
curl http://localhost:4566/restapis/material-api/prod/_user_request_/api/v1/materials
```

### 3. Queue Depth
Check if messages are stuck in queues (consumers down/failing).
```bash
docker exec poc_feature-localstack-1 awslocal sqs list-queues
```
> **Tip:** Use `sqs get-queue-attributes` to see `ApproximateNumberOfMessages`.

---

## Part 2: Failure Analysis (Runbooks)

### Scenario A: Feature Flags System Failure
**Symptom:** Logs show `Failed to evaluate feature flag`. Users see `404 Not Found` on new features.
**Cause:** The `GoFeatureFlag` sidecar is down or unreachable.
**Resolution:**
1. Check container status: `docker ps | grep gofeatureflag`.
2. check logs: `docker logs gofeatureflag`.
3. Restart: `docker compose -f docker/feature-flags/docker-compose.yaml restart`.
4. **Behavior:** The system is fail-safe; flags default to `false`.

### Scenario B: DynamoDB Throttling
**Symptom:** High API latency, logs show `ProvisionedThroughputExceededException`.
**Cause:** Request rate exceeds RCU/WCU limits.
**Resolution:**
1. **Immediate:** The application uses **Polly** to retry with exponential backoff.
2. **Permanent:** Increase capacity or switch to On-Demand mode in Terraform/LocalStack config.

### Scenario C: Cold Start Latency
**Symptom:** The first request after idle time takes > 1s.
**Diagnosis:** Check metric `app.startup_duration_ms > 1000`.
**Resolution:**
- **Dev:** Acceptable behavior in Lambda.
- **Prod:** Configure **Provisioned Concurrency**.
- **Code:** Inspect `AddRuntimeInstrumentation` for JIT/Assembly loading spikes.

### Scenario D: Async Worker Lag
**Symptom:** Updates (e.g., Price Changes) are not reflected immediately.
**Diagnosis:**
- Check SQS Queue Depth.
- Search traces in Grafana (Tempo) to see if the `SNS Publish` span exists, but the `SQS Process` span is missing/delayed.
**Resolution:**
- Check Worker logs: `awslocal logs tail /aws/lambda/PoC-Costing-PriceIngestion`.
- Check DLQ (Dead Letter Queue) for poison messages.

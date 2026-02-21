# 04 - Verification

## How to Verify the Implementation

Once the code changes are applied and deployed to LocalStack:

### 1. Trigger Traffic
Run a test script or manually invoke the API to generate telemetry data.
```powershell
./scripts/tests/test_price_ingestion.ps1
```

### 2. Verify Traces (Tempo)
Access Grafana (http://localhost:3000) -> Explore -> Tempo.
*   **Query**: Search for `service.name="PoC-Costing"`.
*   **Expected Result**: You should seen a trace that spans:
    *   API Gateway (if instrumented or mocked)
    *   **Lambda Function** (`aws.lambda` span) -> *This is what the new package adds!*
    *   ASP.NET Core Controller (`http.server` span)
    *   DynamoDB (`aws.dynamodb` span)

### 3. Verify Logs (Loki)
Access Grafana -> Explore -> Loki.
*   **Query**: `{service_name="PoC-Costing"}`
*   **Expected Result**: structured logs from the application. If `TraceId` is present in the logs, it confirms the correlation injection is working.

### 4. Verify Metrics (Prometheus)
Access Grafana -> Explore -> Prometheus.
*   **Query**: `http_server_request_duration_seconds_count`
*   **Expected Result**: Counters increasing with traffic.

## Troubleshooting
If traces are broken or missing:
1.  **Check Collector Logs**: `docker logs po-feature-otel-collector`
    *   Look for "Connection refused" or OTLP errors.
2.  **Check Lambda Logs**: `aws --endpoint-url=http://localhost:4566 logs tail /aws/lambda/PoC-Costing --follow`
    *   Look for startup errors or OTel exporter errors.
3.  **Inspect Env Vars**: Ensure `OTEL_EXPORTER_OTLP_ENDPOINT` is actually set in the running Lambda:
    ```bash
    aws --endpoint-url=http://localhost:4566 lambda get-function-configuration --function-name PoC-Costing
    ```

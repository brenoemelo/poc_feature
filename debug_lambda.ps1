$ErrorActionPreference = "Stop"
$EndpointUrl = "http://localhost:4566"
$Region = "us-east-1"

Write-Host "--- Materials Ingestion Config ---"
aws lambda get-function-configuration --function-name PoC-Materials-Ingestion --endpoint-url $EndpointUrl --region $Region --output json > materials_config.json
Get-Content materials_config.json

Write-Host "`n--- Costing Ingestion Config ---"
aws lambda get-function-configuration --function-name PoC-Costing-PriceIngestion --endpoint-url $EndpointUrl --region $Region --output json > costing_config.json
Get-Content costing_config.json

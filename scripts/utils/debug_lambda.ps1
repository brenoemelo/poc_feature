$ErrorActionPreference = "Stop"

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
$EndpointUrl = if ($Global:Config) { $Global:Config.Aws.LocalStackUrl } else { "http://localhost:4566" }
$Region = if ($Global:Config) { $Global:Config.Aws.Region } else { "us-east-1" }

Write-Host "--- Materials Ingestion Config ---"
aws lambda get-function-configuration --function-name PoC-Materials-Ingestion --endpoint-url $EndpointUrl --region $Region --output json > ../../config/lambda/materials_config.json
Get-Content ../../config/lambda/materials_config.json

Write-Host "`n--- Costing Ingestion Config ---"
aws lambda get-function-configuration --function-name PoC-Costing-PriceIngestion --endpoint-url $EndpointUrl --region $Region --output json > ../../config/lambda/costing_config.json
Get-Content ../../config/lambda/costing_config.json

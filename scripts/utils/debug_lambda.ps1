$ErrorActionPreference = "Stop"

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }

if (-not $Global:Config) {
    throw "Global Configuration not loaded. Please ensure global.env.ps1 is available."
}

$EndpointUrl = $Global:Config.Aws.LocalStackUrl
$Region = $Global:Config.Aws.Region

Write-Host "--- Materials Ingestion Config ---"
aws lambda get-function-configuration --function-name PoC-Materials-Ingestion --endpoint-url $EndpointUrl --region $Region --output json > ../../config/lambda/materials_config.json
Get-Content ../../config/lambda/materials_config.json

Write-Host "`n--- Costing Ingestion Config ---"
aws lambda get-function-configuration --function-name PoC-Costing-PriceIngestion --endpoint-url $EndpointUrl --region $Region --output json > ../../config/lambda/costing_config.json
Get-Content ../../config/lambda/costing_config.json

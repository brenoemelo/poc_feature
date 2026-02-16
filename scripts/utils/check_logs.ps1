$ErrorActionPreference = "Stop"
$EndpointUrl = "http://localhost:4566"
$Region = "us-east-1"
$FunctionName = "PoC-Costing-PriceIngestion"
$LogGroupName = "/aws/lambda/$FunctionName"

# Invoke
Write-Host "Invoking $FunctionName..."
aws lambda invoke --function-name $FunctionName --payload file://..\..\config\payloads\invoke_payload.json --cli-binary-format raw-in-base64-out ..\..\scratchpad\response.json --endpoint-url $EndpointUrl --region $Region

# Wait for logs
Start-Sleep -Seconds 2

# Get Logs
Write-Host "Fetching logs..."
aws logs filter-log-events --log-group-name $LogGroupName --endpoint-url $EndpointUrl --region $Region --output json > ..\..\scratchpad\logs.json
Get-Content ..\..\scratchpad\logs.json

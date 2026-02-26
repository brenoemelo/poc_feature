$ErrorActionPreference = "Stop"

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }

if (-not $Global:Config) {
    throw "Global Configuration not loaded. Please ensure global.env.ps1 is available."
}

$EndpointUrl = $Global:Config.Aws.LocalStackUrl
$Region = $Global:Config.Aws.Region

$FunctionName = "PoC-Costing-PriceIngestion"
$PayloadPath = "..\..\config\payloads\invoke_payload.json"
$OutputPath = "..\..\scratchpad\response.json"

Write-Host "Checking AWS version..."
cmd /c "aws --version"

Write-Host "Invoking $FunctionName..."
$cmd = "aws lambda invoke --function-name $FunctionName --payload file://$PayloadPath --cli-binary-format raw-in-base64-out $OutputPath --endpoint-url $EndpointUrl --region $Region"
Write-Host "Command: $cmd"

cmd /c $cmd > stdout.txt 2> stderr.txt

Write-Host "Stdout:"
Get-Content stdout.txt
Write-Host "Stderr:"
Get-Content stderr.txt

if (Test-Path $OutputPath) {
    Write-Host "Invocation completed. Response content:"
    Get-Content $OutputPath
}
else {
    Write-Host "Invocation failed: $OutputPath not created."
}

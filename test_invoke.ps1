$ErrorActionPreference = "Stop"
$EndpointUrl = "http://localhost:4566"
$Region = "us-east-1"
$FunctionName = "PoC-Costing-PriceIngestion"
$PayloadPath = "d:\Projetos\poc_feature\payload.json"
$OutputPath = "d:\Projetos\poc_feature\response.json"

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
} else {
    Write-Host "Invocation failed: $OutputPath not created."
}

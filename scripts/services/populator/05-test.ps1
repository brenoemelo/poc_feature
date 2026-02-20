# 05-test.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 5: Smoke Tests" -Level INFO
. "$PSScriptRoot/config.local.ps1"

Write-Log "Invoking Lambda: $($ServiceConfig.Name)" -Level INFO
$PayloadPath = Resolve-Path "$PSScriptRoot/../../../docs/api/payloads/populator_generate_materials.json"
$PayloadContent = Get-Content $PayloadPath -Raw

# Wrap payload in APIGatewayProxyRequest
$Event = @{
    resource = "/{proxy+}"
    path = "/api/v1/populator/jobs"
    httpMethod = "POST"
    headers = @{ "Content-Type" = "application/json" }
    body = $PayloadContent
    isBase64Encoded = $false
}
$EventJson = $Event | ConvertTo-Json -Depth 10
Set-Content "$PSScriptRoot/event.json" $EventJson -Encoding Ascii

$Output = aws lambda invoke --function-name $($ServiceConfig.Name) --payload "fileb://$PSScriptRoot/event.json" --endpoint-url http://localhost:4566 --cli-read-timeout 40 --no-cli-pager "$PSScriptRoot/response.json"
if ($LASTEXITCODE -ne 0) { 
    Write-Log "Lambda Invocation Failed: $Output" -Level ERROR
    throw "Lambda Invocation Failed" 
}

$Response = Get-Content "$PSScriptRoot/response.json" | ConvertFrom-Json
# Check for statusCode (case insensitive)
if ($Response.statusCode -eq 200 -or $Response.StatusCode -eq 200 -or $Response.statusCode -eq 202 -or $Response.StatusCode -eq 202) {
    Write-Log "Smoke Test Passed: Lambda Invocation Successful." -Level SUCCESS
} else {
    Write-Log "Response: $($Response | ConvertTo-Json -Depth 5)" -Level ERROR
    throw "Smoke Test Failed: Invalid Response"
}
Remove-Item "$PSScriptRoot/response.json" -Force
Remove-Item "$PSScriptRoot/event.json" -Force

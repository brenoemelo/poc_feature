# 05-test.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 5: Smoke Tests" -Level INFO
. "$PSScriptRoot/config.local.ps1"

Write-Log "Invoking Lambda: $($ServiceConfig.Name)" -Level INFO
$PayloadPath = Resolve-Path "$PSScriptRoot/../../../docs/api/payloads/costing_calculate_cost.json"
Write-Log "Payload Path: $PayloadPath" -Level INFO

if (-not (Test-Path $PayloadPath)) {
    Write-Log "Payload file does not exist!" -Level ERROR
    exit 1
}

Write-Log "Reading content..." -Level INFO
$PayloadContent = Get-Content $PayloadPath -Raw
# Ensure it's a pure string without PS metadata
$PayloadContent = "$PayloadContent" 
Write-Log "Content read successfully. Length: $($PayloadContent.Length)" -Level INFO

# Wrap payload in APIGatewayProxyRequest
Write-Log "Wrapping payload..." -Level INFO
$Event = @{
    resource = "/{proxy+}"
    path = "/api/v1/costing/estimations"
    httpMethod = "POST"
    headers = @{ "Content-Type" = "application/json" }
    body = $PayloadContent
    isBase64Encoded = $false
}

Write-Log "Converting to JSON..." -Level INFO
try {
    # ConvertTo-Json should work fine with a pure string body
    $EventJson = $Event | ConvertTo-Json -Depth 10
    Write-Log "Json Conversion Done. Length: $($EventJson.Length)" -Level INFO
} catch {
    Write-Log "Json Conversion Failed: $_" -Level ERROR
    exit 1
}

Write-Log "Saving event.json..." -Level INFO
Set-Content "$PSScriptRoot/event.json" $EventJson -Encoding Ascii
Write-Log "Event JSON created at $PSScriptRoot/event.json" -Level INFO

Write-Log "Starting invocation..." -Level INFO
$OldEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    $Output = aws lambda invoke --function-name $($ServiceConfig.Name) --payload "fileb://$PSScriptRoot/event.json" --endpoint-url http://localhost:4566 --cli-read-timeout 40 --no-cli-pager "$PSScriptRoot/response.json" 2>&1
    Write-Log "DEBUG: ExitCode: $LASTEXITCODE" -Level INFO
    Write-Log "DEBUG: Output: $Output" -Level INFO
} catch {
    Write-Log "Caught Exception: $_" -Level ERROR
} finally {
    $ErrorActionPreference = $OldEAP
}

if ($LASTEXITCODE -ne 0) { 
    Write-Log "Lambda Invocation Failed: $Output" -Level ERROR
    throw "Lambda Invocation Failed" 
}

if (-not (Test-Path "$PSScriptRoot/response.json")) {
    Write-Log "Response file not found!" -Level ERROR
    throw "Response file missing"
}

$ResponseContent = Get-Content "$PSScriptRoot/response.json" -Raw
$Response = $ResponseContent | ConvertFrom-Json

# Check for Lambda Execution Error (e.g. Unhandled Exception)
if ($Response.PSObject.Properties.Match('errorType').Count -gt 0) {
    Write-Log "Lambda Execution Error: $($Response.errorMessage)" -Level ERROR
    Write-Log "Full Error: $ResponseContent" -Level ERROR
    exit 1
}

# Check for API Gateway Proxy Response StatusCode
if ($Response.PSObject.Properties.Match('statusCode').Count -gt 0) {
    if ($Response.statusCode -eq 200) {
        Write-Log "Success: $($Response.body)" -Level SUCCESS
    } elseif ($Response.statusCode -eq 400) {
        Write-Log "Success (Validation): Lambda is reachable. Domain returned 400: $($Response.body)" -Level SUCCESS
    } else {
        Write-Log "Failed with StatusCode $($Response.statusCode): $($Response.body)" -Level ERROR
        exit 1
    }
} else {
    Write-Log "Unexpected Response Format: $ResponseContent" -Level ERROR
    exit 1
}
Remove-Item "$PSScriptRoot/response.json" -Force
Remove-Item "$PSScriptRoot/event.json" -Force

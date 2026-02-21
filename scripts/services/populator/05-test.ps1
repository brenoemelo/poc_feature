# 05-test.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 5: Smoke Tests" -Level INFO
. "$PSScriptRoot/config.local.ps1"

$PayloadPath = Resolve-Path "$PSScriptRoot/../../../docs/api/payloads/populator_generate_materials.json"
if (-not (Test-Path $PayloadPath)) {
    throw "Payload file not found: $PayloadPath"
}

$PayloadContent = Get-Content $PayloadPath -Raw
Write-Log "Payload Content Length: $($PayloadContent.Length)" -Level INFO

# Construct APIGatewayProxyRequest Event
$EventObj = @{
    resource = "/{proxy+}"
    path = "/api/v1/populator/jobs"
    httpMethod = "POST"
    headers = @{
        "Content-Type" = "application/json"
        "Accept" = "application/json"
    }
    multiValueHeaders = @{
        "Content-Type" = @("application/json")
    }
    body = $PayloadContent
    isBase64Encoded = $false
    requestContext = @{
        resourceId = "123456"
        apiId = "local-api"
        resourcePath = "/{proxy+}"
        httpMethod = "POST"
        requestId = "c6af9ac6-7b61-11e6-9a41-93e8deadbeef"
        accountId = "123456789012"
        stage = "prod"
        identity = @{
            sourceIp = "127.0.0.1"
            userAgent = "SmokeTest-Agent"
        }
        path = "/api/v1/populator/jobs"
    }
}

$EventJson = $EventObj | ConvertTo-Json -Depth 10
Set-Content "$PSScriptRoot/event.json" $EventJson -Encoding Ascii
Write-Log "Event JSON saved to $PSScriptRoot/event.json" -Level INFO

# Invoke Lambda
$ResponseFile = "$PSScriptRoot/response.json"
if (Test-Path $ResponseFile) { Remove-Item $ResponseFile -Force }

Invoke-LambdaFunction -FunctionName $($ServiceConfig.Name) `
    -PayloadFile "$PSScriptRoot/event.json" `
    -OutputFile $ResponseFile

# Validate Response
$RawResponse = Get-Content $ResponseFile -Raw
Write-Log "Raw Response: $RawResponse" -Level INFO

try {
    $Response = $RawResponse | ConvertFrom-Json
} catch {
    Write-Log "Failed to parse JSON response: $_" -Level ERROR
    throw "JSON Parse Error"
}

# Check statusCode (Case Insensitive)
$StatusCode = if ($Response.PSObject.Properties.Match("statusCode").Count -gt 0) { $Response.statusCode } else { $Response.StatusCode }

if ($StatusCode -eq 200 -or $StatusCode -eq 202) {
    # Validate Body is not empty
    $Body = $Response.body
    if ([string]::IsNullOrWhiteSpace($Body) -or $Body -eq "{}") {
        Write-Log "Smoke Test Failed: Response Body is Empty" -Level ERROR
        throw "Smoke Test Failed: Empty Response Body"
    }
    Write-Log "Smoke Test Passed: Lambda Invocation Successful. Body: $Body" -Level SUCCESS
} else {
    Write-Log "Smoke Test Failed. Response: $($Response | ConvertTo-Json -Depth 5)" -Level ERROR
    throw "Smoke Test Failed: Invalid Status Code $StatusCode"
}

# Cleanup
Remove-Item "$PSScriptRoot/event.json" -Force -ErrorAction SilentlyContinue
Remove-Item "$PSScriptRoot/response.json" -Force -ErrorAction SilentlyContinue

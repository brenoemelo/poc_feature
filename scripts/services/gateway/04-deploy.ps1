# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy API Gateway" -Level INFO
. "$PSScriptRoot/config.local.ps1"

# 1. Create/Get API Gateway using High-Level Function
# Use CustomApiId to ensure stable ID in LocalStack
$ApiId = New-ApiGateway -Name $($ServiceConfig.ApiName) -CustomId $($ServiceConfig.CustomApiId)

if (-not $ApiId) {
    throw "Failed to retrieve or create API Gateway ID."
}

# 2. Update API Definition (OpenAPI)
Write-Log "Updating API Definition from OpenAPI..." -Level INFO
$OpenApiPath = $ServiceConfig.OpenApiPath

if (-not (Test-Path $OpenApiPath)) {
    throw "OpenAPI definition not found at: $OpenApiPath"
}

# We need to read the OpenAPI file and replace placeholders if necessary, 
# but for now we assume it's valid or we just upload it.
# Note: 'aws apigateway put-rest-api' replaces the entire API definition.

Invoke-Aws -Service "apigateway" -Command "put-rest-api" -Arguments @(
    "--rest-api-id", $ApiId,
    "--mode", "overwrite",
    "--body", "fileb://$OpenApiPath"
) | Out-Null

Write-Log "API Definition Updated." -Level SUCCESS

# 3. Deploy to Stage
Write-Log "Deploying API to Stage: $($ServiceConfig.Stage)" -Level INFO

Invoke-Aws -Service "apigateway" -Command "create-deployment" -Arguments @(
    "--rest-api-id", $ApiId,
    "--stage-name", $($ServiceConfig.Stage)
) | Out-Null

Write-Log "Deployment to stage '$($ServiceConfig.Stage)' Successful." -Level SUCCESS

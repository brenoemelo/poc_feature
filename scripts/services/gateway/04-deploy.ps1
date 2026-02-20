# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy" -Level INFO

# Load Config
. "$PSScriptRoot/config.local.ps1"

# Create API Shell (Static ID)
Write-Log "Creating API Gateway Shell: $($ServiceConfig.ApiId)" -Level INFO
$Output = aws apigateway create-rest-api --name $($ServiceConfig.ApiName) --tags "_custom_id_=$($ServiceConfig.ApiId)" --endpoint-url $($ServiceConfig.EndpointUrl) --no-cli-pager 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Log "Failed to create API Gateway Shell: $Output" -Level ERROR
    throw "API Gateway Creation Failed"
}

# Update API Definition (OpenAPI)
Write-Log "Updating API Definition from OpenAPI..." -Level INFO
$Output = aws apigateway put-rest-api --rest-api-id $($ServiceConfig.ApiId) --mode overwrite --body "fileb://$($ServiceConfig.OpenApiPath)" --endpoint-url $($ServiceConfig.EndpointUrl) --no-cli-pager 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Log "Failed to update API Definition: $Output" -Level ERROR
    throw "API Definition Update Failed"
}

# Deploy to Stage
Write-Log "Deploying API to Stage: $($ServiceConfig.Stage)" -Level INFO
$Output = aws apigateway create-deployment --rest-api-id $($ServiceConfig.ApiId) --stage-name $($ServiceConfig.Stage) --endpoint-url $($ServiceConfig.EndpointUrl) --no-cli-pager 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Log "Failed to deploy API to Stage: $Output" -Level ERROR
    throw "API Deployment Failed"
}

Write-Log "Deployment Successful." -Level SUCCESS

# 05-test.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 5: Smoke Tests" -Level INFO

# Load Config
. "$PSScriptRoot/config.local.ps1"

# 1. Check API Existence
Write-Log "Verifying API Gateway: $($ServiceConfig.ApiId)" -Level INFO
$Api = aws apigateway get-rest-api --rest-api-id $($ServiceConfig.ApiId) --endpoint-url $($ServiceConfig.EndpointUrl) --no-cli-pager 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Log "API Gateway Not Found: $Api" -Level ERROR
    throw "Smoke Test Failed: API Gateway Missing"
}
Write-Log "API Gateway Found." -Level SUCCESS

# 2. Check Stage Existence
Write-Log "Verifying Stage: $($ServiceConfig.Stage)" -Level INFO
$Stage = aws apigateway get-stage --rest-api-id $($ServiceConfig.ApiId) --stage-name $($ServiceConfig.Stage) --endpoint-url $($ServiceConfig.EndpointUrl) --no-cli-pager 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Log "Stage Not Found: $Stage" -Level ERROR
    throw "Smoke Test Failed: Stage Missing"
}
Write-Log "Stage Found." -Level SUCCESS

# 3. Optional: Verify URL Reachability (if services are up)
# Since Gateway pipeline runs independently, services might not be up.
# But we can check if the endpoint is listening (even if it returns 500/403).
$ApiUrl = "$($ServiceConfig.EndpointUrl)/restapis/$($ServiceConfig.ApiId)/$($ServiceConfig.Stage)/_user_request_"
Write-Log "API URL: $ApiUrl" -Level INFO

Write-Log "Smoke Tests Passed." -Level SUCCESS

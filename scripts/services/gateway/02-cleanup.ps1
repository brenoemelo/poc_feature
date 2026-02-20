# 02-cleanup.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
. "$PSScriptRoot/../../utils/aws_helpers.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 2: Cleanup (Idempotency)" -Level INFO

# Load Config
. "$PSScriptRoot/config.local.ps1"

# Delete API Gateway
Remove-AwsResource -Description "API Gateway ($($ServiceConfig.ApiId))" -Action {
    aws apigateway delete-rest-api --rest-api-id $($ServiceConfig.ApiId) --endpoint-url $($ServiceConfig.EndpointUrl) --no-cli-pager
}

Write-Log "Cleanup Completed." -Level SUCCESS

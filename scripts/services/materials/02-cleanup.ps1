# 02-cleanup.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
. "$PSScriptRoot/../../utils/aws_helpers.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 2: Cleanup (Idempotency)" -Level INFO

# Load Config
. "$PSScriptRoot/config.local.ps1"

# Delete Lambda
Remove-AwsResource -Description "Lambda ($($ServiceConfig.Name))" -Action {
    aws lambda delete-function --function-name $($ServiceConfig.Name) --endpoint-url http://localhost:4566 --no-cli-pager
}

# Delete Table
Remove-AwsResource -Description "Table ($($ServiceConfig.DynamoTable))" -Action {
    aws dynamodb delete-table --table-name $($ServiceConfig.DynamoTable) --endpoint-url http://localhost:4566 --no-cli-pager
}

Write-Log "Cleanup Completed." -Level SUCCESS

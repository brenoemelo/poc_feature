# 02-cleanup.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
. "$PSScriptRoot/../../utils/aws_helpers.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 2: Cleanup (Idempotency)" -Level INFO

# Load Config
. "$PSScriptRoot/config.local.ps1"

# Delete Lambda
Remove-LambdaFunction -FunctionName $($ServiceConfig.Name)

# Delete Table
Remove-DynamoDbTable -TableName $($ServiceConfig.DynamoTable)

Write-Log "Cleanup Completed." -Level SUCCESS

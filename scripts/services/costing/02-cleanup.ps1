# 02-cleanup.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
. "$PSScriptRoot/../../utils/aws_helpers.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 2: Cleanup (Idempotency)" -Level INFO

# Load Config
. "$PSScriptRoot/config.local.ps1"

# Delete Main Lambda
Remove-LambdaFunction -FunctionName $($ServiceConfig.Name)

# Delete Ingestion Lambda (and its ESMs)
Remove-LambdaFunction -FunctionName $($ServiceConfig.IngestionFunctionName)

# Delete Table
Remove-DynamoDbTable -TableName $($ServiceConfig.DynamoTable)

# Delete Queue
$QueueUrl = "http://localhost:4566/000000000000/$($ServiceConfig.IngestionQueueName)"
Remove-SqsQueue -QueueUrl $QueueUrl

Write-Log "Cleanup Completed." -Level SUCCESS

# 02-cleanup.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
. "$PSScriptRoot/../../utils/aws_helpers.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 2: Cleanup (Idempotency)" -Level INFO

# Load Config
. "$PSScriptRoot/config.local.ps1"

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
$EndpointUrl = if ($Global:Config) { $Global:Config.Aws.LocalStackUrl } else { "http://localhost:4566" }
$AccountId = if ($Global:Config) { $Global:Config.Aws.AccountId } else { "000000000000" }

# Delete Main Lambda
Remove-LambdaFunction -FunctionName $($ServiceConfig.Name)

# Delete Ingestion Lambda (and its ESMs)
Remove-LambdaFunction -FunctionName $($ServiceConfig.IngestionFunctionName)

# Delete Table
Remove-DynamoDbTable -TableName $($ServiceConfig.DynamoTable)

# Delete Queue
$QueueUrl = "$EndpointUrl/$AccountId/$($ServiceConfig.IngestionQueueName)"
Remove-SqsQueue -QueueUrl $QueueUrl

Write-Log "Cleanup Completed." -Level SUCCESS

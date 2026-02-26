# 02-cleanup.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 2: Cleanup (Idempotency)" -Level INFO
. "$PSScriptRoot/config.local.ps1"

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
$EndpointUrl = if ($Global:Config) { $Global:Config.Aws.LocalStackUrl } else { "http://localhost:4566" }
$AccountId = if ($Global:Config) { $Global:Config.Aws.AccountId } else { "000000000000" }

# Remove Main Lambda
Remove-LambdaFunction -FunctionName $($ServiceConfig.Name)

# Remove Worker Lambda
Remove-LambdaFunction -FunctionName $($ServiceConfig.WorkerName)

# Remove SQS Queue
$QueueUrl = "$EndpointUrl/$AccountId/$($ServiceConfig.QueueName)"
Remove-SqsQueue -QueueUrl $QueueUrl

# Remove SNS Topic
Remove-SnsTopic -TopicArn $($ServiceConfig.TopicArn)

Write-Log "Cleanup Completed." -Level SUCCESS

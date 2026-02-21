# 02-cleanup.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 2: Cleanup (Idempotency)" -Level INFO
. "$PSScriptRoot/config.local.ps1"

# Remove Main Lambda
Remove-LambdaFunction -FunctionName $($ServiceConfig.Name)

# Remove Worker Lambda
Remove-LambdaFunction -FunctionName $($ServiceConfig.WorkerName)

# Remove SQS Queue
$QueueUrl = "http://localhost:4566/000000000000/$($ServiceConfig.QueueName)"
Remove-SqsQueue -QueueUrl $QueueUrl

# Remove SNS Topic
Remove-SnsTopic -TopicArn $($ServiceConfig.TopicArn)

Write-Log "Cleanup Completed." -Level SUCCESS

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

# Delete Event Source Mappings (Ingestion)
try {
    $ESMs = aws lambda list-event-source-mappings --function-name $($ServiceConfig.IngestionFunctionName) --endpoint-url http://localhost:4566 --no-cli-pager 2>&1
    if ($LASTEXITCODE -eq 0) {
        $ESMsJson = $ESMs | ConvertFrom-Json
        if ($ESMsJson.EventSourceMappings) {
            foreach ($Mapping in $ESMsJson.EventSourceMappings) {
                Remove-AwsResource -Description "ESM ($($Mapping.UUID))" -Action {
                    aws lambda delete-event-source-mapping --uuid $($Mapping.UUID) --endpoint-url http://localhost:4566 --no-cli-pager
                }
            }
        }
    }
} catch {
    Write-Log "Failed to list ESMs: $_" -Level WARN
}

# Delete Lambda (Ingestion)
Remove-AwsResource -Description "Lambda ($($ServiceConfig.IngestionFunctionName))" -Action {
    aws lambda delete-function --function-name $($ServiceConfig.IngestionFunctionName) --endpoint-url http://localhost:4566 --no-cli-pager
}

# Delete Table
Remove-AwsResource -Description "Table ($($ServiceConfig.DynamoTable))" -Action {
    aws dynamodb delete-table --table-name $($ServiceConfig.DynamoTable) --endpoint-url http://localhost:4566 --no-cli-pager
}

# Delete Queue
$QueueUrl = "http://localhost:4566/000000000000/$($ServiceConfig.IngestionQueueName)"
Remove-AwsResource -Description "Queue ($($ServiceConfig.IngestionQueueName))" -Action {
    aws sqs delete-queue --queue-url $QueueUrl --endpoint-url http://localhost:4566 --no-cli-pager
}

Write-Log "Cleanup Completed." -Level SUCCESS

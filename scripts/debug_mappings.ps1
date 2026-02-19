
. "$PSScriptRoot\..\deployment\localstack\utils.ps1"
Initialize-Environment

$FunctionName = "PoC-Materials-Ingestion"
$IngestionQueueArn = "arn:aws:sqs:us-east-1:000000000000:materials-ingestion-queue"

Write-Host "Checking mappings for $FunctionName..."
$mappings = Invoke-Aws -Service "lambda" -Command "list-event-source-mappings" -Arguments @("--function-name", $FunctionName) -JsonOutput $true -IgnoreError $true
Write-Host "Mappings (Function): $($mappings | ConvertTo-Json -Depth 5)"

Write-Host "Checking mappings for ARN $IngestionQueueArn..."
$mappingsArn = Invoke-Aws -Service "lambda" -Command "list-event-source-mappings" -Arguments @("--event-source-arn", $IngestionQueueArn) -JsonOutput $true -IgnoreError $true
Write-Host "Mappings (ARN): $($mappingsArn | ConvertTo-Json -Depth 5)"

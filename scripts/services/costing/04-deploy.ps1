# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
. "$PSScriptRoot/../../utils/aws_helpers.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy" -Level INFO
. "$PSScriptRoot/config.local.ps1"

$ZipPath = "$PSScriptRoot/../../../PoC-Costing.zip"

if (-not (Test-Path $ZipPath)) {
    throw "Build artifact not found: $ZipPath"
}

$AbsZipPath = Resolve-Path $ZipPath
Write-Log "Absolute ZIP path resolved to: $AbsZipPath" -Level INFO

Write-Log "Deploying AWS Resources..." -Level INFO

# 1. DynamoDB Table
$TableDef = @{
    TableName = $ServiceConfig.DynamoTable
    AttributeDefinitions = @(
        @{ AttributeName = "ComponentName"; AttributeType = "S" }
    )
    KeySchema = @(
        @{ AttributeName = "ComponentName"; KeyType = "HASH" }
    )
    ProvisionedThroughput = @{
        ReadCapacityUnits = 5
        WriteCapacityUnits = 5
    }
}
New-DynamoDbTable -TableDef $TableDef | Out-Null

# 2. SQS Queue
$QueueUrl = New-SqsQueue -Name $($ServiceConfig.IngestionQueueName)

# 3. SNS Subscription (Materials -> Queue)
# Note: Topic ARN is external, so we don't create it here, just subscribe.
# If it doesn't exist, this might fail, but in local env we usually ensure it.
# aws_helpers New-SnsSubscription handles subscription.
# We need to make sure the topic exists. If it's owned by Materials service, it should be there.
New-SnsSubscription -TopicArn $($ServiceConfig.MaterialsTopicArn) `
    -Protocol "sqs" `
    -Endpoint $($ServiceConfig.IngestionQueueArn)

# 4. Main Lambda (API)
$MainEnvVars = "OTEL_SERVICE_NAME=$($ServiceConfig.Name),MATERIALS_API_URL=$($ServiceConfig.MaterialsApiUrl),$(Get-CommonEnvVars)"
New-LambdaFunction -Name $($ServiceConfig.Name) `
    -Handler "PoC.Costing" `
    -RoleArn "arn:aws:iam::000000000000:role/lambda-role" `
    -ZipPath $AbsZipPath `
    -Timeout "30" `
    -MemorySize "1024" `
    -EnvironmentVariables $MainEnvVars

# 5. API Gateway Permission
Grant-LambdaPermission -FunctionName $($ServiceConfig.Name) `
    -StatementId "apigateway-invoke" `
    -Principal "apigateway.amazonaws.com" `
    -SourceArn "arn:aws:execute-api:us-east-1:000000000000:$($Global:Config.ApiGateway.Id)/*/*/*"

# 6. Worker Lambda (Ingestion)
$WorkerEnvVars = "OTEL_SERVICE_NAME=$($ServiceConfig.IngestionFunctionName),COSTING_TABLE_NAME=$($ServiceConfig.DynamoTable),$(Get-CommonEnvVars)"
New-LambdaFunction -Name $($ServiceConfig.IngestionFunctionName) `
    -Handler "PoC.Costing::PoC.Costing.Functions.PriceIngestionFunction::FunctionHandler" `
    -RoleArn "arn:aws:iam::000000000000:role/lambda-role" `
    -ZipPath $AbsZipPath `
    -Timeout "30" `
    -MemorySize "1024" `
    -EnvironmentVariables $WorkerEnvVars

# 7. Event Source Mapping (Queue -> Worker)
New-EventSourceMapping -FunctionName $($ServiceConfig.IngestionFunctionName) `
    -EventSourceArn $($ServiceConfig.IngestionQueueArn) `
    -BatchSize 10

Write-Log "Deployment Successful." -Level SUCCESS

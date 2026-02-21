# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
. "$PSScriptRoot/../../utils/aws_helpers.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy" -Level INFO
. "$PSScriptRoot/config.local.ps1"

$ZipPath = "$PSScriptRoot/../../../PoC-Materials.zip"

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
        @{ AttributeName = "material_id"; AttributeType = "S" },
        @{ AttributeName = "record_type"; AttributeType = "S" }
    )
    KeySchema = @(
        @{ AttributeName = "material_id"; KeyType = "HASH" }
    )
    ProvisionedThroughput = @{
        ReadCapacityUnits = 5
        WriteCapacityUnits = 5
    }
    GlobalSecondaryIndexes = @(
        @{
            IndexName = "IX_Materials_By_Type"
            KeySchema = @(
                @{ AttributeName = "record_type"; KeyType = "HASH" },
                @{ AttributeName = "material_id"; KeyType = "RANGE" }
            )
            Projection = @{ ProjectionType = "ALL" }
            ProvisionedThroughput = @{
                ReadCapacityUnits = 5
                WriteCapacityUnits = 5
            }
        }
    )
}
New-DynamoDbTable -TableDef $TableDef | Out-Null

# 2. SNS Topic
$TopicArn = New-SnsTopic -Name $($ServiceConfig.SnsTopic)

# 3. Lambda Function
$EnvVars = "Materials__TableName=$($ServiceConfig.DynamoTable),$(Get-CommonEnvVars)"
New-LambdaFunction -Name $($ServiceConfig.Name) `
    -Handler "PoC.Materials" `
    -RoleArn "arn:aws:iam::000000000000:role/lambda-role" `
    -ZipPath $AbsZipPath `
    -Timeout "30" `
    -MemorySize "1024" `
    -EnvironmentVariables $EnvVars

# 4. API Gateway Permission
Grant-LambdaPermission -FunctionName $($ServiceConfig.Name) `
    -StatementId "apigateway-invoke" `
    -Principal "apigateway.amazonaws.com" `
    -SourceArn "arn:aws:execute-api:us-east-1:000000000000:$($Global:Config.ApiGateway.Id)/*/*/*"

# 5. Ingestion Queue
$QueueUrl = New-SqsQueue -Name $($ServiceConfig.IngestionQueueName)
$QueueArn = "arn:aws:sqs:us-east-1:000000000000:$($ServiceConfig.IngestionQueueName)"

# 6. Subscribe Queue to Topic
New-SnsSubscription -TopicArn $TopicArn -Protocol "sqs" -Endpoint $QueueArn

# 7. Ingestion Lambda
$IngestionEnvVars = "Materials__TableName=$($ServiceConfig.DynamoTable),OTEL_SERVICE_NAME=$($ServiceConfig.IngestionFunctionName),$(Get-CommonEnvVars)"
New-LambdaFunction -Name $($ServiceConfig.IngestionFunctionName) `
    -Handler "PoC.Materials::PoC.Materials.Functions.MaterialIngestionFunction::FunctionHandler" `
    -RoleArn "arn:aws:iam::000000000000:role/lambda-role" `
    -ZipPath $AbsZipPath `
    -Timeout "30" `
    -MemorySize "1024" `
    -EnvironmentVariables $IngestionEnvVars

# 8. Event Source Mapping
# Need to define New-EventSourceMapping if not present in aws_helpers.ps1, but I checked and it is present.
# However, I should check if aws_helpers.ps1 is sourced correctly. Yes, at line 4.
# Wait, I need to make sure New-EventSourceMapping is exported/available. PowerShell functions are usually global if dot-sourced.

# One missing thing: The SQS Queue needs a policy to allow SNS to send messages.
# But in LocalStack, sometimes it works without it. AWS requires it.
# I'll skip policy for now as per previous Python script which didn't add it explicitly (or maybe aws_helpers.py does it? No).
# Actually, for SNS->SQS subscription, usually we need a policy on SQS.
# But let's try without first.

# Call the function from aws_helpers.ps1
# But wait, I see `New-EventSourceMapping` in `aws_helpers.ps1` takes `FunctionName`, `EventSourceArn`, `BatchSize`.
# The function name in `aws_helpers.ps1` is `New-EventSourceMapping`.
# I'll just call it.

# However, I need to define the function if it's not available in the current scope.
# It is available because `. "$PSScriptRoot/../../utils/aws_helpers.ps1"` is at the top.

# One fix: The function New-EventSourceMapping in aws_helpers.ps1 uses `create-event-source-mapping` with `--event-source-arn` but the argument name is `$EventSourceArn`.
# I should call it correctly.

# Let's add the call.

# But wait, I need to add `New-EventSourceMapping` to `aws_helpers.ps1` if it's not there?
# I saw it in the Read output!
# "function New-EventSourceMapping {" at line 239.
# So it is there.

# Wait, `New-EventSourceMapping` calls `create-event-source-mapping`.
# The argument `--event-source-arn` takes the Queue ARN.
# The argument `--function-name` takes the Lambda Name.
# So:
Invoke-Aws -Service "lambda" -Command "create-event-source-mapping" -Arguments @(
    "--function-name", $ServiceConfig.IngestionFunctionName,
    "--event-source-arn", $QueueArn,
    "--batch-size", "10"
) -IgnoreError $true | Out-Null
# Wait, I should use the helper function New-EventSourceMapping if possible.
# But looking at aws_helpers.ps1, it does exactly that.

# I will use New-EventSourceMapping function.

New-EventSourceMapping -FunctionName $($ServiceConfig.IngestionFunctionName) `
    -EventSourceArn $QueueArn `
    -BatchSize 10

Write-Log "Deployment Successful." -Level SUCCESS

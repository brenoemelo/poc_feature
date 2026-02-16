
# -----------------------------------------------------------------------------
# Costing Service Deployment Script
# Deploys PoC.Costing Lambda and API configuration to LocalStack.
# -----------------------------------------------------------------------------

# -----------------------------------------------------------------------------
# Parameters & Configuration
# -----------------------------------------------------------------------------
param(
    [string]$ProjectPath = "src\PoC.Costing\PoC.Costing.csproj",
    [string]$PublishDir = "publish\PoC.Costing",
    [string]$ZipPath = "PoC.Costing.zip",
    [string]$FunctionName = "PoC-Costing",
    [string]$EndpointUrl = "http://localhost:4566",
    [string]$Region = "us-east-1"
)

# Load shared utilities
. "$PSScriptRoot\utils.ps1"

# Initialize environment and safety checks
Initialize-Environment -EndpointUrl $EndpointUrl -Region $Region
Assert-AwsConnection
Assert-NotProduction

# Resolve paths relative to repo root
$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$ProjectPath = Join-Path $RepoRoot $ProjectPath
$PublishDir = Join-Path $RepoRoot $PublishDir
$ZipPath = Join-Path $RepoRoot $ZipPath

# -----------------------------------------------------------------------------
# Build & Package
# -----------------------------------------------------------------------------
Write-Log "Starting Build & Package for $FunctionName..." "Info"

if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
dotnet publish $ProjectPath -c Release -o $PublishDir -r linux-x64 --self-contained false
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }

if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath
$AbsZipPath = (Resolve-Path $ZipPath).Path

Write-Log "Build successful. Artifact: $AbsZipPath" "Success"

# -----------------------------------------------------------------------------
# Cleanup (Clean Slate Strategy)
# -----------------------------------------------------------------------------
Write-Log "--- STARTING 100% CLEANUP ---" "Warning"

# Delete Functions
Ensure-LambdaDeleted -FunctionName $FunctionName
$IngestionFunctionName = "PoC-Costing-PriceIngestion"
$IngestionQueueArn = "arn:aws:sqs:${Region}:000000000000:costing-ingestion-queue"
Ensure-LambdaDeleted -FunctionName $IngestionFunctionName -EventSourceArn $IngestionQueueArn

# Delete Queues
$QueueUrl = "$EndpointUrl/000000000000/costing-ingestion-queue"
Ensure-SqsDeleted -QueueUrl $QueueUrl

# Delete Tables
Ensure-DynamoDbTableDeleted -TableName "costing-prices-table"

Write-Log "--- CLEANUP COMPLETED ---" "Success"

# -----------------------------------------------------------------------------
# Resource Creation
# -----------------------------------------------------------------------------

# 1. Create DynamoDB Table
Write-Log "Creating DynamoDB Table: costing-prices-table" "Info"
Invoke-Aws -Service "dynamodb" -Command "create-table" -Arguments @(
    "--table-name", "costing-prices-table",
    "--attribute-definitions", "AttributeName=ComponentName,AttributeType=S",
    "--key-schema", "AttributeName=ComponentName,KeyType=HASH",
    "--provisioned-throughput", "ReadCapacityUnits=5,WriteCapacityUnits=5"
) | Out-Null

# 2. Create Main Lambda Function
Write-Log "Creating Lambda function: $FunctionName" "Info"
Invoke-Aws -Service "lambda" -Command "create-function" -Arguments @(
    "--function-name", $FunctionName,
    "--runtime", "dotnet8",
    "--handler", "PoC.Costing",
    "--role", "arn:aws:iam::000000000000:role/lambda-role",
    "--zip-file", "fileb://$AbsZipPath",
    "--timeout", "30",
    "--memory-size", "512",
    "--environment", "Variables={MATERIALS_API_URL=http://172.17.0.1:4566/restapis/material-api/prod/_user_request_/,AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}"
) | Out-Null

# 3. Create Price Ingestion Function
Write-Log "Creating Price Ingestion function: $IngestionFunctionName" "Info"
Invoke-Aws -Service "lambda" -Command "create-function" -Arguments @(
    "--function-name", $IngestionFunctionName,
    "--runtime", "dotnet8",
    "--handler", "PoC.Costing::PoC.Costing.Functions.PriceIngestionFunction::FunctionHandler",
    "--role", "arn:aws:iam::000000000000:role/lambda-role",
    "--zip-file", "fileb://$AbsZipPath",
    "--timeout", "30",
    "--memory-size", "512",
    "--environment", "Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}"
) | Out-Null

# 4. Create SQS Queue
Write-Log "Creating SQS Queue: costing-ingestion-queue" "Info"
Invoke-Aws -Service "sqs" -Command "create-queue" -Arguments @("--queue-name", "costing-ingestion-queue") | Out-Null

# 5. Subscribe Queue to Topic (material-events)
Write-Log "Subscribing Queue to Topic..." "Info"
$TopicArn = "arn:aws:sns:us-east-1:000000000000:material-events"
$QueueArn = "arn:aws:sqs:us-east-1:000000000000:costing-ingestion-queue"
Invoke-Aws -Service "sns" -Command "subscribe" -Arguments @(
    "--topic-arn", $TopicArn,
    "--protocol", "sqs",
    "--notification-endpoint", $QueueArn
) | Out-Null

# 6. Create Event Source Mapping
Write-Log "Configuring Event Source Mapping for $IngestionFunctionName..." "Info"
Invoke-Aws -Service "lambda" -Command "create-event-source-mapping" -Arguments @(
    "--function-name", $IngestionFunctionName,
    "--batch-size", "10",
    "--event-source-arn", $QueueArn
) | Out-Null

# 7. Configure Function URL & Public Access
Write-Log "Configuring Function URL for $FunctionName..." "Info"
Invoke-Aws -Service "lambda" -Command "create-function-url-config" -Arguments @(
    "--function-name", $FunctionName,
    "--auth-type", "NONE"
) -IgnoreError $true | Out-Null

Invoke-Aws -Service "lambda" -Command "add-permission" -Arguments @(
    "--function-name", $FunctionName,
    "--statement-id", "FunctionURLAllowPublicAccess",
    "--action", "lambda:InvokeFunctionUrl",
    "--principal", "*",
    "--function-url-auth-type", "NONE"
) -IgnoreError $true | Out-Null

Write-Log "Deployment for PoC.Costing completed successfully!" "Success"

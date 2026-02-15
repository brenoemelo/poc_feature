
# -----------------------------------------------------------------------------
# Materials Service Deployment Script
# Deploys PoC.Materials Lambda, API, and Event Infrastructure to LocalStack.
# -----------------------------------------------------------------------------

# -----------------------------------------------------------------------------
# Parameters & Configuration
# -----------------------------------------------------------------------------
param(
    [string]$ProjectPath = "src\PoC.Materials/PoC.Materials.csproj",
    [string]$PublishDir = "publish\PoC.Materials",
    [string]$ZipPath = "PoC.Materials.zip",
    [string]$FunctionName = "PoC-Materials",
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
$IngestionFunctionName = "PoC-Materials-Ingestion"
$IngestionQueueArn = "arn:aws:sqs:${Region}:000000000000:materials-ingestion-queue"
Ensure-LambdaDeleted -FunctionName $IngestionFunctionName -EventSourceArn $IngestionQueueArn

# Delete Queues and Topics
$TopicArn = "arn:aws:sns:us-east-1:000000000000:material-events"
Ensure-SnsTopicDeleted -TopicArn $TopicArn

$QueueUrl = "$EndpointUrl/000000000000/materials-ingestion-queue"
Ensure-SqsDeleted -QueueUrl $QueueUrl

# Delete Conflicting S3 Bucket (from init-aws.sh or previous runs)
# This prevents S3 vs API Gateway Custom Domain routing conflicts for "materials" path
Ensure-S3BucketDeleted -BucketName "materials"

Write-Log "--- CLEANUP COMPLETED ---" "Success"

# -----------------------------------------------------------------------------
# Resource Creation
# -----------------------------------------------------------------------------

# 1. Create Main Lambda Function
Write-Log "Creating Lambda function: $FunctionName" "Info"
Invoke-Aws -Service "lambda" -Command "create-function" -Arguments @(
    "--function-name", $FunctionName,
    "--runtime", "dotnet8",
    "--handler", "PoC.Materials",
    "--role", "arn:aws:iam::000000000000:role/lambda-role",
    "--zip-file", "fileb://$AbsZipPath",
    "--timeout", "30",
    "--memory-size", "512",
    "--environment", "Variables={MATERIALS_TABLE_NAME=materials-table,AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}"
) | Out-Null

# 2. Create Ingestion Worker Function
Write-Log "Creating Ingestion function: $IngestionFunctionName" "Info"
Invoke-Aws -Service "lambda" -Command "create-function" -Arguments @(
    "--function-name", $IngestionFunctionName,
    "--runtime", "dotnet8",
    "--handler", "PoC.Materials::PoC.Materials.Functions.MaterialIngestionFunction::FunctionHandler",
    "--role", "arn:aws:iam::000000000000:role/lambda-role",
    "--zip-file", "fileb://$AbsZipPath",
    "--timeout", "30",
    "--memory-size", "512",
    "--environment", "Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}"
) | Out-Null

# 3. Configure Function URL & Public Access
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

# 4. Create SNS Topic
Write-Log "Creating SNS Topic: material-events" "Info"
Invoke-Aws -Service "sns" -Command "create-topic" -Arguments @("--name", "material-events") | Out-Null

# 5. Create SQS Queue
Write-Log "Creating SQS Queue: materials-ingestion-queue" "Info"
Invoke-Aws -Service "sqs" -Command "create-queue" -Arguments @("--queue-name", "materials-ingestion-queue") | Out-Null

# 6. Subscribe Queue to Topic
Write-Log "Subscribing Queue to Topic..." "Info"
$QueueArn = "arn:aws:sqs:us-east-1:000000000000:materials-ingestion-queue"
Invoke-Aws -Service "sns" -Command "subscribe" -Arguments @(
    "--topic-arn", $TopicArn,
    "--protocol", "sqs",
    "--notification-endpoint", $QueueArn
) | Out-Null

# 7. Create Event Source Mapping
Write-Log "Configuring Event Source Mapping for $IngestionFunctionName..." "Info"
Invoke-Aws -Service "lambda" -Command "create-event-source-mapping" -Arguments @(
    "--function-name", $IngestionFunctionName,
    "--batch-size", "10",
    "--event-source-arn", $QueueArn
) | Out-Null

Write-Log "Deployment for PoC.Materials completed successfully!" "Success"


# -----------------------------------------------------------------------------
# Populator Service Deployment Script
# Deploys PoC.Populator Lambda and Worker to LocalStack.
# -----------------------------------------------------------------------------

# -----------------------------------------------------------------------------
# Parameters & Configuration
# -----------------------------------------------------------------------------
param(
    [string]$ProjectPath = "src\PoC.Populator/PoC.Populator.csproj",
    [string]$PublishDir = "publish\PoC.Populator",
    [string]$ZipPath = "PoC.Populator.zip",
    [string]$FunctionName = "PoC-Populator",
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
dotnet publish $ProjectPath -c Release -o $PublishDir -r linux-x64 --self-contained false -p:PublishReadyToRun=false
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
$WorkerFunctionName = "PoC-Populator-Worker"
$WorkerQueueArn = "arn:aws:sqs:${Region}:000000000000:populator-queue"
Ensure-LambdaDeleted -FunctionName $WorkerFunctionName -EventSourceArn $WorkerQueueArn

# Delete Queues and Topics
$TopicArn = "arn:aws:sns:us-east-1:000000000000:population-requests"
Ensure-SnsTopicDeleted -TopicArn $TopicArn

$QueueUrl = "$EndpointUrl/000000000000/populator-queue"
Ensure-SqsDeleted -QueueUrl $QueueUrl

Write-Log "--- CLEANUP COMPLETED ---" "Success"

# -----------------------------------------------------------------------------
# Resource Creation
# -----------------------------------------------------------------------------

# 1. Create Main Lambda Function
Write-Log "Creating Lambda function: $FunctionName" "Info"
Invoke-Aws -Service "lambda" -Command "create-function" -Arguments @(
    "--function-name", $FunctionName,
    "--runtime", "dotnet8",
    "--handler", "PoC.Populator",
    "--role", "arn:aws:iam::000000000000:role/lambda-role",
    "--zip-file", "fileb://$AbsZipPath",
    "--timeout", "30",
    "--memory-size", "1024",
    "--environment", "Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test,$(Get-CommonEnvVars)}"
) | Out-Null

# 2. Create Worker Function
Write-Log "Creating Worker function: $WorkerFunctionName" "Info"
Invoke-Aws -Service "lambda" -Command "create-function" -Arguments @(
    "--function-name", $WorkerFunctionName,
    "--runtime", "dotnet8",
    "--handler", "PoC.Populator::PoC.Populator.Functions.PopulatorWorkerFunction::FunctionHandler",
    "--role", "arn:aws:iam::000000000000:role/lambda-role",
    "--zip-file", "fileb://$AbsZipPath",
    "--timeout", "60",
    "--memory-size", "1024",
    "--environment", "Variables={MATERIALS_API_URL=http://localstack:4566/restapis/material-api/prod/_user_request_/,SNS_TOPIC_ARN=arn:aws:sns:us-east-1:000000000000:material-events,AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test,$(Get-CommonEnvVars)}"
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
Write-Log "Creating SNS Topic: population-requests" "Info"
Invoke-Aws -Service "sns" -Command "create-topic" -Arguments @("--name", "population-requests") | Out-Null

# 5. Create SQS Queue
Write-Log "Creating SQS Queue: populator-queue" "Info"
Invoke-Aws -Service "sqs" -Command "create-queue" -Arguments @("--queue-name", "populator-queue") | Out-Null

# 6. Subscribe Queue to Topic
Write-Log "Subscribing Queue to Topic..." "Info"
$QueueArn = "arn:aws:sqs:us-east-1:000000000000:populator-queue"
Invoke-Aws -Service "sns" -Command "subscribe" -Arguments @(
    "--topic-arn", $TopicArn,
    "--protocol", "sqs",
    "--notification-endpoint", $QueueArn,
    "--attributes", "RawMessageDelivery=true"
) | Out-Null

# 7. Create Event Source Mapping
Write-Log "Configuring Event Source Mapping for $WorkerFunctionName..." "Info"
Invoke-Aws -Service "lambda" -Command "create-event-source-mapping" -Arguments @(
    "--function-name", $WorkerFunctionName,
    "--batch-size", "10",
    "--event-source-arn", $QueueArn
) | Out-Null

Write-Log "Deployment for PoC.Populator completed successfully!" "Success"

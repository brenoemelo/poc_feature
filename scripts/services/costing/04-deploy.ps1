# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy" -Level INFO
. "$PSScriptRoot/config.local.ps1"

$ZipPath = "$PSScriptRoot/../../../$($ServiceConfig.Name).zip"

# Create Table
Write-Log "Creating DynamoDB Table..." -Level INFO
aws dynamodb create-table --table-name $($ServiceConfig.DynamoTable) --attribute-definitions AttributeName=ComponentName,AttributeType=S --key-schema AttributeName=ComponentName,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 --endpoint-url http://localhost:4566 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "DynamoDB Table Creation Failed" }

# Create Queue
Write-Log "Creating SQS Queue: $($ServiceConfig.IngestionQueueName)" -Level INFO
aws sqs create-queue --queue-name $($ServiceConfig.IngestionQueueName) --endpoint-url http://localhost:4566 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "SQS Queue Creation Failed" }

# Subscribe Queue to Material Events Topic
Write-Log "Subscribing Queue to Topic..." -Level INFO
aws sns subscribe --topic-arn $($ServiceConfig.MaterialsTopicArn) --protocol sqs --notification-endpoint $($ServiceConfig.IngestionQueueArn) --endpoint-url http://localhost:4566 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "SNS Subscription Failed" }

# Create Main Lambda
Write-Log "Creating Main Lambda Function..." -Level INFO
aws lambda create-function --function-name $($ServiceConfig.Name) --runtime dotnet10 --handler PoC.Costing --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$ZipPath --environment "Variables={OTEL_SERVICE_NAME=$($ServiceConfig.Name),MATERIALS_API_URL=$($ServiceConfig.MaterialsApiUrl),Otel__Endpoint=http://otel-collector:4318,FeatureFlags__UnleashApiUrl=http://unleash:4242/api/,AWS__Region=us-east-1}" --endpoint-url http://localhost:4566 --timeout 30 --memory-size 1024 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Main Lambda Creation Failed" }

# Add API Gateway Permission (Main)
Write-Log "Adding API Gateway Permission..." -Level INFO
aws lambda add-permission --function-name $($ServiceConfig.Name) --statement-id apigateway-invoke --action lambda:InvokeFunction --principal apigateway.amazonaws.com --source-arn "arn:aws:execute-api:us-east-1:000000000000:$($Global:Config.ApiGateway.Id)/*/*/*" --endpoint-url http://localhost:4566 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "API Gateway Permission Failed" }

# Create Ingestion Lambda
Write-Log "Creating Ingestion Lambda Function..." -Level INFO
aws lambda create-function --function-name $($ServiceConfig.IngestionFunctionName) --runtime dotnet10 --handler PoC.Costing::PoC.Costing.Functions.PriceIngestionFunction::FunctionHandler --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$ZipPath --environment "Variables={OTEL_SERVICE_NAME=$($ServiceConfig.IngestionFunctionName),COSTING_TABLE_NAME=$($ServiceConfig.DynamoTable),Otel__Endpoint=http://otel-collector:4318,FeatureFlags__UnleashApiUrl=http://unleash:4242/api/,AWS__Region=us-east-1}" --endpoint-url http://localhost:4566 --timeout 30 --memory-size 1024 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Ingestion Lambda Creation Failed" }

# Create Event Source Mapping
Write-Log "Creating Event Source Mapping..." -Level INFO
$ErrorActionPreference = "Continue"
$Output = aws lambda create-event-source-mapping --function-name $($ServiceConfig.IngestionFunctionName) --batch-size 10 --event-source-arn $($ServiceConfig.IngestionQueueArn) --endpoint-url http://localhost:4566 --no-cli-pager 2>&1
$ExitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"

if ($ExitCode -ne 0) { 
    Write-Log "Error Output: $Output" -Level ERROR
    throw "Event Source Mapping Creation Failed" 
}

Write-Log "Deployment Successful." -Level SUCCESS

# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy" -Level INFO
. "$PSScriptRoot/config.local.ps1"

$ZipPath = "$PSScriptRoot/../../../$($ServiceConfig.Name).zip"

# Create Topic
Write-Log "Creating SNS Topic: $($ServiceConfig.TopicName)" -Level INFO
aws sns create-topic --name $($ServiceConfig.TopicName) --endpoint-url http://localhost:4566 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "SNS Topic Creation Failed" }

# Create Queue
Write-Log "Creating SQS Queue: $($ServiceConfig.QueueName)" -Level INFO
aws sqs create-queue --queue-name $($ServiceConfig.QueueName) --endpoint-url http://localhost:4566 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "SQS Queue Creation Failed" }

# Subscribe Queue to Topic
Write-Log "Subscribing Queue to Topic..." -Level INFO
aws sns subscribe --topic-arn $($ServiceConfig.TopicArn) --protocol sqs --notification-endpoint $($ServiceConfig.QueueArn) --attributes RawMessageDelivery=true --endpoint-url http://localhost:4566 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "SNS Subscription Failed" }

# Create Main Lambda (API)
Write-Log "Creating Main Lambda Function (API)..." -Level INFO
aws lambda create-function --function-name $($ServiceConfig.Name) --runtime dotnet10 --handler PoC.Populator --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$ZipPath --environment "Variables={MATERIALS_API_URL=$($ServiceConfig.MaterialsApiUrl),SNS_TOPIC_ARN=$($ServiceConfig.TopicArn),Otel__Endpoint=http://otel-collector:4318,FeatureFlags__UnleashApiUrl=http://unleash:4242/api/,AWS__Region=us-east-1}" --endpoint-url http://localhost:4566 --timeout 30 --memory-size 1024 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Main Lambda Creation Failed" }

# Add API Gateway Permission (Main)
Write-Log "Adding API Gateway Permission..." -Level INFO
aws lambda add-permission --function-name $($ServiceConfig.Name) --statement-id apigateway-invoke --action lambda:InvokeFunction --principal apigateway.amazonaws.com --source-arn "arn:aws:execute-api:us-east-1:000000000000:$($Global:Config.ApiGateway.Id)/*/*/*" --endpoint-url http://localhost:4566 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "API Gateway Permission Failed" }

# Create Worker Lambda
Write-Log "Creating Worker Lambda Function..." -Level INFO
aws lambda create-function --function-name $($ServiceConfig.WorkerName) --runtime dotnet10 --handler PoC.Populator::PoC.Populator.Functions.PopulatorWorkerFunction::FunctionHandler --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$ZipPath --environment "Variables={MATERIALS_API_URL=$($ServiceConfig.MaterialsApiUrl),SNS_TOPIC_ARN=$($ServiceConfig.TopicArn),Otel__Endpoint=http://otel-collector:4318,FeatureFlags__UnleashApiUrl=http://unleash:4242/api/,AWS__Region=us-east-1}" --endpoint-url http://localhost:4566 --timeout 30 --memory-size 1024 --no-cli-pager 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Worker Lambda Creation Failed" }

# Create Event Source Mapping
Write-Log "Creating Event Source Mapping..." -Level INFO
$Output = aws lambda create-event-source-mapping --function-name $($ServiceConfig.WorkerName) --batch-size 10 --event-source-arn $($ServiceConfig.QueueArn) --endpoint-url http://localhost:4566 --no-cli-pager 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Log "Error Output: $Output" -Level ERROR
    throw "Event Source Mapping Creation Failed"
}

Write-Log "Deployment Successful." -Level SUCCESS

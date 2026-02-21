# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy" -Level INFO
. "$PSScriptRoot/config.local.ps1"

# 1. Resolve ZIP file path to a valid absolute path on Windows
$RelativeZipPath = "$PSScriptRoot/../../../$($ServiceConfig.Name).zip"

if (-Not (Test-Path $RelativeZipPath)) {
    Write-Log "ZIP file not found at resolved path: $RelativeZipPath" -Level ERROR
    throw "ZIP File Not Found"
}

$ZipPath = (Resolve-Path $RelativeZipPath).ProviderPath
Write-Log "Absolute ZIP path resolved to: $ZipPath" -Level INFO

# 2. Deploy AWS Resources using High-Level Functions (DRY Pattern)
Write-Log "Deploying AWS Resources..." -Level INFO

# Ensure SNS Topic
New-SnsTopic -Name $($ServiceConfig.TopicName) | Out-Null

# Ensure SQS Queue
New-SqsQueue -Name $($ServiceConfig.QueueName) | Out-Null

# Ensure SNS Subscription (Queue -> Topic)
New-SnsSubscription -TopicArn $($ServiceConfig.TopicArn) `
    -Protocol "sqs" `
    -Endpoint $($ServiceConfig.QueueArn) `
    -Attributes @{ "RawMessageDelivery" = "true" }

# Deploy Main Lambda (API)
Write-Log "Deploying Main Lambda (API)..." -Level INFO
$MainEnv = "MATERIALS_API_URL=$($ServiceConfig.MaterialsApiUrl),SNS_TOPIC_ARN=$($ServiceConfig.TopicArn),$(Get-CommonEnvVars)"

New-LambdaFunction -Name $($ServiceConfig.Name) `
    -Handler "PoC.Populator" `
    -RoleArn "arn:aws:iam::000000000000:role/lambda-role" `
    -ZipPath $ZipPath `
    -Runtime "dotnet8" `
    -Timeout "30" `
    -MemorySize "1024" `
    -EnvironmentVariables $MainEnv

# Grant API Gateway Permission
Grant-LambdaPermission -FunctionName $($ServiceConfig.Name) `
    -StatementId "apigateway-invoke" `
    -Principal "apigateway.amazonaws.com" `
    -SourceArn "arn:aws:execute-api:us-east-1:000000000000:$($Global:Config.ApiGateway.Id)/*/*/*"

# Deploy Worker Lambda
Write-Log "Deploying Worker Lambda..." -Level INFO
$WorkerEnv = "MATERIALS_API_URL=$($ServiceConfig.MaterialsApiUrl),SNS_TOPIC_ARN=$($ServiceConfig.TopicArn),$(Get-CommonEnvVars)"

New-LambdaFunction -Name $($ServiceConfig.WorkerName) `
    -Handler "PoC.Populator::PoC.Populator.Functions.PopulatorWorkerFunction::FunctionHandler" `
    -RoleArn "arn:aws:iam::000000000000:role/lambda-role" `
    -ZipPath $ZipPath `
    -Runtime "dotnet8" `
    -Timeout "30" `
    -MemorySize "1024" `
    -EnvironmentVariables $WorkerEnv

# Create Event Source Mapping (Worker <- Queue)
New-EventSourceMapping -FunctionName $($ServiceConfig.WorkerName) `
    -EventSourceArn $($ServiceConfig.QueueArn) `
    -BatchSize 10

Write-Log "Deployment Successful." -Level SUCCESS

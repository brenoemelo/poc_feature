param(
    [string]$ProjectPath = "src\PoC.Materials/PoC.Materials.csproj",
    [string]$PublishDir = "publish\PoC.Materials",
    [string]$ZipPath = "PoC.Materials.zip",
    [string]$ContainerName = "poc_feature-localstack-1",
    [string]$FunctionName = "PoC-Materials",
    [string]$EndpointUrl = "http://localhost:4566",
    [string]$Region = "us-east-1"
)

$env:AWS_ACCESS_KEY_ID = "test"
$env:AWS_SECRET_ACCESS_KEY = "test"
$env:AWS_DEFAULT_REGION = "us-east-1"
$env:AWS_PAGER = ""

Write-Host "Publishing project..."
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
dotnet publish $ProjectPath -c Release -o $PublishDir -r linux-x64 --self-contained false

Write-Host "Zipping artifacts..."
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath

$AbsZipPath = (Resolve-Path $ZipPath).Path

Write-Host "Checking if function exists ($FunctionName)..."
$process = Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda get-function --function-name $FunctionName" -Wait -NoNewWindow -PassThru
$exitCode = $process.ExitCode

if ($exitCode -eq 0) {
    Write-Host "Function $FunctionName exists. Updating code and configuration..."
    
    # Update Code
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-code --function-name $FunctionName --zip-file fileb://$AbsZipPath" -Wait -NoNewWindow

    # Update Configuration
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-configuration --function-name $FunctionName --timeout 30 --memory-size 512 --environment Variables={MATERIALS_TABLE_NAME=materials-table,AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
} else {
    Write-Host "Creating new function ($FunctionName)..."
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-function --function-name $FunctionName --runtime dotnet8 --handler PoC.Materials --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$AbsZipPath --timeout 30 --memory-size 512 --environment Variables={MATERIALS_TABLE_NAME=materials-table,AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
}

# Deploy Ingestion Worker
$IngestionFunctionName = "PoC-Materials-Ingestion"
Write-Host "Checking if ingestion function exists ($IngestionFunctionName)..."
$processIngest = Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda get-function --function-name $IngestionFunctionName" -Wait -NoNewWindow -PassThru
$exitCodeIngest = $processIngest.ExitCode

if ($exitCodeIngest -eq 0) {
    Write-Host "Function $IngestionFunctionName exists. Updating code and configuration..."
    
    # Update Code
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-code --function-name $IngestionFunctionName --zip-file fileb://$AbsZipPath" -Wait -NoNewWindow

    # Update Configuration
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-configuration --function-name $IngestionFunctionName --handler PoC.Materials::PoC.Materials.Functions.MaterialIngestionFunction::FunctionHandler --timeout 30 --memory-size 512 --environment Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
} else {
    Write-Host "Creating Ingestion function ($IngestionFunctionName)..."
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-function --function-name $IngestionFunctionName --runtime dotnet8 --handler PoC.Materials::PoC.Materials.Functions.MaterialIngestionFunction::FunctionHandler --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$AbsZipPath --timeout 30 --memory-size 512 --environment Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
}

Write-Host "Configuring Function URL..."
$processUrl = Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda get-function-url-config --function-name $FunctionName" -Wait -NoNewWindow -PassThru
if ($processUrl.ExitCode -ne 0) {
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-function-url-config --function-name $FunctionName --auth-type NONE" -Wait -NoNewWindow
} else {
    Write-Host "Function URL already exists."
}

Write-Host "Adding public access permission..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda add-permission --function-name $FunctionName --statement-id FunctionURLAllowPublicAccess --action lambda:InvokeFunctionUrl --principal * --function-url-auth-type NONE" -Wait -NoNewWindow

Write-Host "Creating SNS Topic material-events (ensure exists)..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager sns create-topic --name material-events" -Wait -NoNewWindow

Write-Host "Creating SQS Queue materials-ingestion-queue..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager sqs create-queue --queue-name materials-ingestion-queue" -Wait -NoNewWindow

Write-Host "Subscribing materials-ingestion-queue to material-events topic..."
$TopicArn = "arn:aws:sns:us-east-1:000000000000:material-events"
$QueueArn = "arn:aws:sqs:us-east-1:000000000000:materials-ingestion-queue"
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager sns subscribe --topic-arn $TopicArn --protocol sqs --notification-endpoint $QueueArn" -Wait -NoNewWindow

Write-Host "Configuring Event Source Mapping for $IngestionFunctionName..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-event-source-mapping --function-name $IngestionFunctionName --batch-size 10 --event-source-arn $QueueArn" -Wait -NoNewWindow

Write-Host "Deployment for PoC.Materials completed."

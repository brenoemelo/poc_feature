param(
    [string]$ProjectPath = "src\PoC.Populator\PoC.Populator.csproj",
    [string]$PublishDir = "publish\PoC.Populator",
    [string]$ZipPath = "publish\PoC.Populator.zip",
    [string]$ContainerName = "poc_feature-localstack-1",
    [string]$FunctionName = "PoC-Populator",
    [string]$EndpointUrl = "http://localhost:4566",
    [string]$Region = "us-east-1"
)

$env:AWS_ACCESS_KEY_ID = "test"
$env:AWS_SECRET_ACCESS_KEY = "test"
$env:AWS_DEFAULT_REGION = "us-east-1"
$env:AWS_PAGER = ""

Write-Host "Publishing PoC.Populator..."
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
dotnet publish $ProjectPath -c Release -o $PublishDir -r linux-x64 --self-contained false

Write-Host "Zipping artifacts..."
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath

$AbsZipPath = (Resolve-Path $ZipPath).Path
Write-Host "Zip Path: $AbsZipPath"

Write-Host "Creating SNS Topic material-events..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager sns create-topic --name material-events" -Wait -NoNewWindow

Write-Host "Creating SQS Queue populator-queue..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager sqs create-queue --queue-name populator-queue" -Wait -NoNewWindow

Write-Host "Checking if function exists ($FunctionName)..."
$process = Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda get-function --function-name $FunctionName" -Wait -NoNewWindow -PassThru
$exitCode = $process.ExitCode

if ($exitCode -eq 0) {
    Write-Host "Function $FunctionName exists. Updating code and configuration..."
    
    # Update Code
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-code --function-name $FunctionName --zip-file fileb://$AbsZipPath" -Wait -NoNewWindow

    # Update Configuration
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-configuration --function-name $FunctionName --timeout 30 --memory-size 512 --environment Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
} else {
    Write-Host "Creating Lambda function ($FunctionName)..."
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-function --function-name $FunctionName --runtime dotnet8 --handler PoC.Populator --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$AbsZipPath --timeout 30 --memory-size 512 --environment Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
}

Write-Host "Configuring Function URL..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-function-url-config --function-name $FunctionName --auth-type NONE" -Wait -NoNewWindow

Write-Host "Adding public access permission..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda add-permission --function-name $FunctionName --statement-id FunctionURLAllowPublicAccess --action lambda:InvokeFunctionUrl --principal * --function-url-auth-type NONE" -Wait -NoNewWindow

# Deploy Populator Worker
$WorkerFunctionName = "PoC-Populator-Worker"
Write-Host "Checking if worker function exists ($WorkerFunctionName)..."
$processWorker = Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda get-function --function-name $WorkerFunctionName" -Wait -NoNewWindow -PassThru
$exitCodeWorker = $processWorker.ExitCode

if ($exitCodeWorker -eq 0) {
    Write-Host "Function $WorkerFunctionName exists. Updating code and configuration..."
    
    # Update Code
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-code --function-name $WorkerFunctionName --zip-file fileb://$AbsZipPath" -Wait -NoNewWindow

    # Update Configuration
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-configuration --function-name $WorkerFunctionName --handler PoC.Populator::PoC.Populator.Functions.PopulatorWorkerFunction::FunctionHandler --timeout 30 --memory-size 512 --environment Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,SNS_TOPIC_ARN=arn:aws:sns:us-east-1:000000000000:material-events,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
} else {
    Write-Host "Creating Worker function ($WorkerFunctionName)..."
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-function --function-name $WorkerFunctionName --runtime dotnet8 --handler PoC.Populator::PoC.Populator.Functions.PopulatorWorkerFunction::FunctionHandler --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$AbsZipPath --timeout 30 --memory-size 512 --environment Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,SNS_TOPIC_ARN=arn:aws:sns:us-east-1:000000000000:material-events,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
}

Write-Host "Configuring Event Source Mapping for $WorkerFunctionName..."
$QueueArn = "arn:aws:sqs:us-east-1:000000000000:populator-queue"
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-event-source-mapping --function-name $WorkerFunctionName --batch-size 10 --event-source-arn $QueueArn" -Wait -NoNewWindow

Write-Host "Deployment for PoC.Populator completed."

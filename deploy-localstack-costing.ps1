param(
    [string]$ProjectPath = "src\PoC.Costing\PoC.Costing.csproj",
    [string]$PublishDir = "publish\PoC.Costing",
    [string]$ZipPath = "publish\PoC.Costing.zip",
    [string]$ContainerName = "poc_feature-localstack-1",
    [string]$FunctionName = "PoC-Costing",
    [string]$EndpointUrl = "http://localhost:4566",
    [string]$Region = "us-east-1"
)

$env:AWS_ACCESS_KEY_ID = "test"
$env:AWS_SECRET_ACCESS_KEY = "test"
$env:AWS_DEFAULT_REGION = "us-east-1"
$env:AWS_PAGER = ""

Write-Host "Publishing PoC.Costing..."
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
dotnet publish $ProjectPath -c Release -o $PublishDir -r linux-x64 --self-contained false

Write-Host "Zipping artifacts..."
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath

$AbsZipPath = (Resolve-Path $ZipPath).Path
Write-Host "Zip Path: $AbsZipPath"

Write-Host "Checking if function exists ($FunctionName)..."
$process = Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda get-function --function-name $FunctionName" -Wait -NoNewWindow -PassThru
$exitCode = $process.ExitCode

Write-Host "AWS Get-Function Exit Code: $exitCode"

if ($exitCode -eq 0) {
    Write-Host "Function $FunctionName exists. Updating code and configuration..."
    
    # Update Code
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-code --function-name $FunctionName --zip-file fileb://$AbsZipPath" -Wait -NoNewWindow

    # Update Configuration
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda update-function-configuration --function-name $FunctionName --timeout 30 --memory-size 512 --environment Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
} else {
    Write-Host "Creating Lambda function ($FunctionName)..."
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-function --function-name $FunctionName --runtime dotnet8 --handler PoC.Costing --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$AbsZipPath --timeout 30 --memory-size 512 --environment Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}" -Wait -NoNewWindow
}

Write-Host "Configuring Function URL..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda create-function-url-config --function-name $FunctionName --auth-type NONE" -Wait -NoNewWindow

Write-Host "Adding public access permission..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda add-permission --function-name $FunctionName --statement-id FunctionURLAllowPublicAccess --action lambda:InvokeFunctionUrl --principal * --function-url-auth-type NONE" -Wait -NoNewWindow

Write-Host "Deployment for PoC.Costing completed."

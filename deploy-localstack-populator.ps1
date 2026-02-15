param(
    [string]$ProjectPath = "src\PoC.Populator\PoC.Populator.csproj",
    [string]$PublishDir = "publish\PoC.Populator",
    [string]$ZipPath = "publish\PoC.Populator.zip",
    [string]$ContainerName = "poc_feature-localstack-1",
    [string]$FunctionName = "PoC-Populator"
)

Write-Host "Publishing PoC.Populator..."
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
dotnet publish $ProjectPath -c Release -o $PublishDir -r linux-x64 --self-contained false

Write-Host "Zipping artifacts..."
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath

Write-Host "Copying zip to LocalStack container..."
docker cp $ZipPath "$ContainerName:/tmp/populator.zip"

Write-Host "Deleting existing function (if any)..."
docker exec $ContainerName awslocal lambda delete-function --function-name $FunctionName 2>$null

Write-Host "Creating Lambda function..."
docker exec $ContainerName awslocal lambda create-function `
    --function-name $FunctionName `
    --runtime dotnet8 `
    --handler PoC.Populator `
    --role arn:aws:iam::000000000000:role/lambda-role `
    --zip-file fileb:///tmp/populator.zip `
    --timeout 30 `
    --memory-size 512 `
    --environment "Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}"

Write-Host "Configuring Function URL..."
docker exec $ContainerName awslocal lambda create-function-url-config `
    --function-name $FunctionName `
    --auth-type NONE

Write-Host "Deployment for PoC.Populator completed."

# Deploy PoC.Materials to LocalStack

$projectPath = "src/PoC.Materials/PoC.Materials.csproj"
$publishDir = "publish/PoC.Materials"
$zipFile = "PoC.Materials.zip"
$functionName = "PoC-Materials"
$containerName = "poc_feature-localstack-1"

# 1. Clean and Publish
Write-Host "Publishing project..."
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish $projectPath -c Release -o $publishDir -r linux-x64 --self-contained false

# 2. Zip
Write-Host "Zipping artifacts..."
if (Test-Path $zipFile) { Remove-Item -Force $zipFile }
Compress-Archive -Path "$publishDir/*" -DestinationPath $zipFile

# 3. Copy to LocalStack container
Write-Host "Copying zip to LocalStack container..."
docker cp $zipFile "$($containerName):/tmp/function.zip"

# 4. Check if function exists and delete if so
$functionExists = docker exec $containerName awslocal lambda get-function --function-name $functionName 2>$null
if ($functionExists) {
    Write-Host "Deleting existing function..."
    docker exec $containerName awslocal lambda delete-function --function-name $functionName
}

Write-Host "Creating new function..."
docker exec $containerName awslocal lambda create-function `
    --function-name $functionName `
    --runtime dotnet8 `
    --handler PoC.Materials `
    --role arn:aws:iam::000000000000:role/lambda-role `
    --zip-file fileb:///tmp/function.zip `
    --timeout 30 `
    --memory-size 512 `
    --environment "Variables={MATERIALS_TABLE_NAME=materials-table,AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1}"

# 5. Configure Function URL
Write-Host "Configuring Function URL..."
$urlConfig = docker exec $containerName awslocal lambda get-function-url-config --function-name $functionName 2>$null

if (-not $urlConfig) {
    $result = docker exec $containerName awslocal lambda create-function-url-config `
        --function-name $functionName `
        --auth-type NONE
    Write-Host "Function URL created: $result"
} else {
    Write-Host "Function URL already exists."
    Write-Host $urlConfig
}

Write-Host "Deployment complete!"

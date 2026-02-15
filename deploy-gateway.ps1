param(
    [string]$EndpointUrl = "http://localhost:4566",
    [string]$Region = "us-east-1"
)

$env:AWS_ACCESS_KEY_ID = "test"
$env:AWS_SECRET_ACCESS_KEY = "test"
$env:AWS_DEFAULT_REGION = "us-east-1"
$env:AWS_PAGER = ""

$apiName = "Material Formulation API"
Write-Host "Checking if API Gateway exists ($apiName)..."

# Use aws to get REST APIs
$process = Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager apigateway get-rest-apis" -Wait -NoNewWindow -PassThru
# We need the output. Start-Process doesn't easily capture output to variable without redirect.
# So I'll use invoke-expression or direct execution for capturing output.
$apisJson = aws --endpoint-url $EndpointUrl --region $Region --no-cli-pager apigateway get-rest-apis
$apis = $apisJson | ConvertFrom-Json

$apiId = $null
if ($apis.items) {
    $existingApi = $apis.items | Where-Object { $_.name -eq $apiName }
    if ($existingApi) {
        $apiId = $existingApi.id
        Write-Host "Found existing REST API: $apiId"
    }
}

$OpenApiFile = (Resolve-Path "docs/openapi.yaml").Path
# Ensure forward slashes for AWS CLI if needed, or just use normal path. AWS CLI on Windows handles backslashes usually, but file URL might need care.
# fileb:// accepts path.

if ($apiId) {
    Write-Host "Updating REST API..."
    # Update existing API
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager apigateway put-rest-api --rest-api-id $apiId --mode overwrite --body fileb://$OpenApiFile" -Wait -NoNewWindow
} else {
    Write-Host "Creating new REST API..."
    # Create new API
    $creationOutput = aws --endpoint-url $EndpointUrl --region $Region --no-cli-pager apigateway import-rest-api --body fileb://$OpenApiFile
    $creationResult = $creationOutput | ConvertFrom-Json
    $apiId = $creationResult.id
}

if (-not $apiId) {
    Write-Error "Failed to get API ID"
    exit 1
}

Write-Host "Deploying API to 'prod' stage..."
Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager apigateway create-deployment --rest-api-id $apiId --stage-name prod" -Wait -NoNewWindow

# Add Lambda Permissions
$functions = @("PoC-Materials", "PoC-Costing", "PoC-Populator")
foreach ($func in $functions) {
    Write-Host "Adding permission for $func..."
    # Remove existing permission if any (ignore error)
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda remove-permission --function-name $func --statement-id apigateway-invoke-$func" -Wait -NoNewWindow 2>$null
    
    # Add permission
    Start-Process -FilePath "aws" -ArgumentList "--endpoint-url $EndpointUrl --region $Region --no-cli-pager lambda add-permission --function-name $func --statement-id apigateway-invoke-$func --action lambda:InvokeFunction --principal apigateway.amazonaws.com --source-arn arn:aws:execute-api:us-east-1:000000000000:$apiId/*/*/*" -Wait -NoNewWindow
}

Write-Host "REST API Deployed Successfully!"
Write-Host "Base URL: http://localhost:4566/restapis/$apiId/prod/_user_request_/"

# Update test_all_apis.ps1 with the new API ID
$TestScriptPath = "test_all_apis.ps1"
if (Test-Path $TestScriptPath) {
    $content = Get-Content $TestScriptPath
    $newContent = $content -replace '\$ApiId = ".*"', '$ApiId = "' + $apiId + '"'
    Set-Content $TestScriptPath $newContent
    Write-Host "Updated $TestScriptPath with new API ID: $apiId"
}


# -----------------------------------------------------------------------------
# Costing Service Deployment Script
# Deploys PoC.Costing Lambda and API configuration to LocalStack.
# -----------------------------------------------------------------------------

# -----------------------------------------------------------------------------
# Parameters & Configuration
# -----------------------------------------------------------------------------
param(
    [string]$ProjectPath = "src\PoC.Costing\PoC.Costing.csproj",
    [string]$PublishDir = "publish\PoC.Costing",
    [string]$ZipPath = "PoC.Costing.zip",
    [string]$FunctionName = "PoC-Costing",
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
dotnet publish $ProjectPath -c Release -o $PublishDir -r linux-x64 --self-contained false
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }

if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath
$AbsZipPath = (Resolve-Path $ZipPath).Path

Write-Log "Build successful. Artifact: $AbsZipPath" "Success"

# -----------------------------------------------------------------------------
# Cleanup (Clean Slate Strategy)
# -----------------------------------------------------------------------------
Write-Log "--- STARTING 100% CLEANUP ---" "Warning"

Ensure-LambdaDeleted -FunctionName $FunctionName

Write-Log "--- CLEANUP COMPLETED ---" "Success"

# -----------------------------------------------------------------------------
# Resource Creation
# -----------------------------------------------------------------------------

# 1. Create Lambda Function
Write-Log "Creating Lambda function: $FunctionName" "Info"
Invoke-Aws -Service "lambda" -Command "create-function" -Arguments @(
    "--function-name", $FunctionName,
    "--runtime", "dotnet8",
    "--handler", "PoC.Costing",
    "--role", "arn:aws:iam::000000000000:role/lambda-role",
    "--zip-file", "fileb://$AbsZipPath",
    "--timeout", "30",
    "--memory-size", "512",
    "--environment", "Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}"
) | Out-Null

# 2. Configure Function URL & Public Access
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

Write-Log "Deployment for PoC.Costing completed successfully!" "Success"

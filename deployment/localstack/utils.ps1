
# -----------------------------------------------------------------------------
# Utility Script for LocalStack Deployment
# Contains shared functions for logging, error handling, retries, and safety checks.
# -----------------------------------------------------------------------------

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# -----------------------------------------------------------------------------
# Logging Functions
# -----------------------------------------------------------------------------
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "Info"
    )

    $color = "White"
    switch ($Level) {
        "Success" { $color = "Green" }
        "Warning" { $color = "Yellow" }
        "Error" { $color = "Red" }
        "Info" { $color = "Cyan" }
        "Debug" { $color = "Gray" }
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# -----------------------------------------------------------------------------
# AWS Execution Wrapper with Retry
# -----------------------------------------------------------------------------
function Invoke-Aws {
    param(
        [string]$Service,
        [string]$Command,
        [string[]]$Arguments = @(),
        [hashtable]$Environment = @{},
        [bool]$JsonOutput = $false,
        [bool]$IgnoreError = $false,
        [int]$MaxRetries = 3
    )

    $cmdLine = "aws --endpoint-url $env:AWS_ENDPOINT_URL --region $env:AWS_DEFAULT_REGION --no-cli-pager $Service $Command $Arguments"
    
    # Mask secrets in logs (basic heuristic)
    $logCmd = $cmdLine
    if ($logCmd -match "AWS_SECRET_ACCESS_KEY") {
        $logCmd = $logCmd -replace "AWS_SECRET_ACCESS_KEY=.*", "AWS_SECRET_ACCESS_KEY=***"
    }
    
    Write-Log "Executing: $Service $Command" "Debug"

    $retryCount = 0
    $success = $false
    $lastError = $null

    while (-not $success -and $retryCount -lt $MaxRetries) {
        try {
            # Construct the argument list for Start-Process
            # We need to explicitly include global flags here because we are running 'aws' executable directly
            $finalArgs = @(
                "--endpoint-url", $env:AWS_ENDPOINT_URL,
                "--region", $env:AWS_DEFAULT_REGION,
                "--no-cli-pager",
                $Service,
                $Command
            ) + $Arguments

            $stdoutFile = "stdout_$($PID)_$($retryCount).tmp"
            $stderrFile = "stderr_$($PID)_$($retryCount).tmp"

            $process = Start-Process -FilePath "aws" -ArgumentList $finalArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
            
            $stdout = if (Test-Path $stdoutFile) { Get-Content $stdoutFile -Raw } else { "" }
            $stderr = if (Test-Path $stderrFile) { Get-Content $stderrFile -Raw } else { "" }
            
            Remove-Item $stdoutFile -ErrorAction SilentlyContinue
            Remove-Item $stderrFile -ErrorAction SilentlyContinue

            if ($process.ExitCode -eq 0) {
                $success = $true
                if ($JsonOutput) {
                    if ([string]::IsNullOrWhiteSpace($stdout)) { return $null }
                    return ($stdout | ConvertFrom-Json)
                }
                return $stdout
            }
            else {
                # Check if it's a "ResourceNotFound" error which might be acceptable
                if ($IgnoreError -and ($stderr -match "NotFound" -or $stderr -match "NoSuch" -or $stderr -match "does not exist")) {
                    Write-Log "Ignored error: $stderr" "Debug"
                    return $null
                }
                throw "Exit code $($process.ExitCode): $stderr"
            }
        }
        catch {
            $lastError = $_
            $retryCount++
            if ($retryCount -lt $MaxRetries) {
                $backoff = [Math]::Pow(2, $retryCount)
                Write-Log "Command failed. Retrying in $backoff seconds... ($lastError)" "Warning"
                Start-Sleep -Seconds $backoff
            }
        }
    }

    if (-not $success -and -not $IgnoreError) {
        Write-Log "Command failed after $MaxRetries attempts: $cmdLine" "Error"
        throw $lastError
    }
}

# -----------------------------------------------------------------------------
# Environment & Safety Checks
# -----------------------------------------------------------------------------
function Initialize-Environment {
    param(
        [string]$EndpointUrl = "http://localhost:4566",
        [string]$Region = "us-east-1"
    )

    $env:AWS_ACCESS_KEY_ID = "test"
    $env:AWS_SECRET_ACCESS_KEY = "test"
    $env:AWS_DEFAULT_REGION = $Region
    $env:AWS_ENDPOINT_URL = $EndpointUrl
    $env:AWS_PAGER = ""

    Write-Log "Environment initialized for LocalStack ($EndpointUrl)" "Info"
}

function Assert-AwsConnection {
    Write-Log "Verifying AWS connectivity..." "Info"
    try {
        Invoke-Aws -Service "sts" -Command "get-caller-identity" -JsonOutput $true | Out-Null
        Write-Log "Connection verified." "Success"
    }
    catch {
        Write-Log "Failed to connect to AWS endpoint ($env:AWS_ENDPOINT_URL). Is LocalStack running?" "Error"
        exit 1
    }
}

function Assert-NotProduction {
    if ($env:AWS_ENDPOINT_URL -notmatch "localhost" -and $env:AWS_ENDPOINT_URL -notmatch "127.0.0.1") {
        Write-Log "DANGER: You are attempting to run destructive scripts against a non-local environment ($env:AWS_ENDPOINT_URL)." "Error"
        Write-Log "This script is strictly for LocalStack/Development." "Error"
        exit 1
    }
}

function Get-RepoRoot {
    return (Resolve-Path "$PSScriptRoot\..\..").Path
}

function Get-CommonEnvVars {
    $otel = "OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4318,OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf,Otel__Endpoint=http://otel-collector:4318,Otel__Protocol=http,OTEL_RESOURCE_ATTRIBUTES=deployment.environment=local,OTEL_METRICS_EXPORTER=otlp,OTEL_LOGS_EXPORTER=otlp"
    $flags = "FeatureFlags__Provider=Unleash,FeatureFlags__UnleashApiUrl=http://unleash:4242/api/,FeatureFlags__UnleashApiKey=*:development.unleash-insecure-api-token,FeatureFlags__UnleashAppName=poc-app,FeatureFlags__UnleashInstanceId=local-lambda,FeatureFlags__FetchTogglesIntervalSeconds=1,FeatureFlags__TimeoutSeconds=10"
    $serilog = "Serilog__MinimumLevel=Debug,Serilog__MinimumLevel__Override__PoC=Debug"
    return "$otel,$flags,$serilog"
}

# -----------------------------------------------------------------------------
# Common Resource Helpers
# -----------------------------------------------------------------------------
function Ensure-LambdaDeleted {
    param(
        [string]$FunctionName,
        [string]$EventSourceArn = $null
    )
    
    Write-Log "Ensuring Lambda function '$FunctionName' is deleted..." "Info"
    
    # 1. Remove Event Source Mappings (triggers) first to avoid dependency errors
    # Check by Function Name
    try {
        $mappings = Invoke-Aws -Service "lambda" -Command "list-event-source-mappings" -Arguments @("--function-name", $FunctionName) -JsonOutput $true -IgnoreError $true
        
        if ($mappings -and $mappings.EventSourceMappings) {
            foreach ($mapping in $mappings.EventSourceMappings) {
                Write-Log "Deleting event source mapping (by function): $($mapping.UUID)" "Info"
                Invoke-Aws -Service "lambda" -Command "delete-event-source-mapping" -Arguments @("--uuid", $mapping.UUID) -IgnoreError $true | Out-Null
            }
        }
    }
    catch {
        Write-Log "Error checking event source mappings by function (ignoring): $_" "Warning"
    }

    # Check by Event Source ARN (if provided)
    if (-not [string]::IsNullOrEmpty($EventSourceArn)) {
        try {
            $mappings = Invoke-Aws -Service "lambda" -Command "list-event-source-mappings" -Arguments @("--event-source-arn", $EventSourceArn) -JsonOutput $true -IgnoreError $true
            
            if ($mappings -and $mappings.EventSourceMappings) {
                foreach ($mapping in $mappings.EventSourceMappings) {
                    Write-Log "Deleting event source mapping (by source): $($mapping.UUID)" "Info"
                    Invoke-Aws -Service "lambda" -Command "delete-event-source-mapping" -Arguments @("--uuid", $mapping.UUID) -IgnoreError $true | Out-Null
                }
            }
        }
        catch {
            Write-Log "Error checking event source mappings by source (ignoring): $_" "Warning"
        }
    }

    # 2. Delete the function
    Invoke-Aws -Service "lambda" -Command "delete-function" -Arguments @("--function-name", $FunctionName) -IgnoreError $true | Out-Null
}

function Ensure-SqsDeleted {
    param(
        [string]$QueueUrl
    )
    
    Write-Log "Ensuring SQS Queue '$QueueUrl' is deleted..." "Info"
    Invoke-Aws -Service "sqs" -Command "delete-queue" -Arguments @("--queue-url", $QueueUrl) -IgnoreError $true | Out-Null
}

function Ensure-SnsTopicDeleted {
    param(
        [string]$TopicArn
    )
    
    Write-Log "Ensuring SNS Topic '$TopicArn' is deleted..." "Info"
    Invoke-Aws -Service "sns" -Command "delete-topic" -Arguments @("--topic-arn", $TopicArn) -IgnoreError $true | Out-Null
}

function Ensure-DynamoDbTableDeleted {
    param(
        [string]$TableName
    )
    
    Write-Log "Ensuring DynamoDB Table '$TableName' is deleted..." "Info"
    Invoke-Aws -Service "dynamodb" -Command "delete-table" -Arguments @("--table-name", $TableName) -IgnoreError $true | Out-Null
}

function Ensure-S3BucketDeleted {
    param(
        [string]$BucketName
    )
    
    Write-Log "Ensuring S3 Bucket '$BucketName' is deleted..." "Info"
    
    # Check if bucket exists
    try {
        $buckets = Invoke-Aws -Service "s3api" -Command "list-buckets" -JsonOutput $true -IgnoreError $true
        $bucketExists = $false
        if ($buckets -and $buckets.Buckets) {
            $bucketExists = ($buckets.Buckets | Where-Object { $_.Name -eq $BucketName })
        }
        
        if ($bucketExists) {
            Write-Log "Bucket '$BucketName' exists. Deleting..." "Warning"
            # Force delete (remove all objects first)
            Invoke-Aws -Service "s3" -Command "rb" -Arguments @("s3://$BucketName", "--force") -IgnoreError $true | Out-Null
            Write-Log "Bucket '$BucketName' deleted." "Success"
        }
        else {
            Write-Log "Bucket '$BucketName' does not exist." "Info"
        }
    }
    catch {
        Write-Log "Error deleting bucket '$BucketName': $_" "Warning"
    }
}

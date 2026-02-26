# AWS Helpers

function Get-AwsIdentity {
    param([string]$EndpointUrl)
    aws sts get-caller-identity --endpoint-url $EndpointUrl --cli-connect-timeout 5 --cli-read-timeout 10 --no-cli-pager
}

function Assert-AwsConnection {
    param([string]$EndpointUrl)
    
    # Load Global Config
    $GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
    if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
    if (-not $EndpointUrl) {
        $EndpointUrl = if ($Global:Config) { $Global:Config.Aws.LocalStackUrl } else { "http://localhost:4566" }
    }

    Write-Log "Verifying AWS Connection to $EndpointUrl..." -Level INFO
    aws sts get-caller-identity --endpoint-url $EndpointUrl --cli-connect-timeout 5 --cli-read-timeout 10 --no-cli-pager 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Log "AWS Connection Verified." -Level SUCCESS
    } else {
        Write-Log "Failed to connect to AWS/LocalStack at $EndpointUrl." -Level ERROR
        throw "AWS Connection Failed"
    }
}

function Remove-AwsResource {
    param(
        [string]$Description,
        [scriptblock]$Action
    )
    
    Write-Log "Removing $Description..." -Level INFO
    # Temporarily relax ErrorActionPreference to capture CLI errors without throwing immediately
    $OldEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    
    try {
        $Output = & $Action 2>&1
        if ($LASTEXITCODE -ne 0) {
            $ErrorText = $Output | Out-String
            if ($ErrorText -match "ResourceNotFoundException" -or $ErrorText -match "NotFound" -or $ErrorText -match "does not exist") {
                Write-Log "Resource $Description not found (already deleted)." -Level INFO
            } else {
                Write-Log "Cleanup Warning for ${Description}: $ErrorText" -Level WARN
            }
        }
    } catch {
        Write-Log "Cleanup Exception for ${Description}: $_" -Level WARN
    } finally {
        $ErrorActionPreference = $OldEAP
    }
}

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

    $EndpointUrl = if ($env:AWS_ENDPOINT_URL) { $env:AWS_ENDPOINT_URL } else { "http://localhost:4566" }
    $Region = if ($env:AWS_DEFAULT_REGION) { $env:AWS_DEFAULT_REGION } else { "us-east-1" }

    $cmdLine = "aws --endpoint-url $EndpointUrl --region $Region --no-cli-pager $Service $Command $Arguments"
    
    # Mask secrets in logs (basic heuristic)
    $logCmd = $cmdLine
    if ($logCmd -match "AWS_SECRET_ACCESS_KEY") {
        $logCmd = $logCmd -replace "AWS_SECRET_ACCESS_KEY=.*", "AWS_SECRET_ACCESS_KEY=***"
    }
    
    Write-Log "Executing: $Service $Command" -Level INFO

    $retryCount = 0
    $success = $false
    $lastError = $null

    while (-not $success -and $retryCount -lt $MaxRetries) {
        try {
            # Construct the argument list for Start-Process
            # We need to explicitly include global flags here because we are running 'aws' executable directly
            $finalArgs = @(
                "--endpoint-url", $EndpointUrl,
                "--region", $Region,
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
                    Write-Log "Ignored error: $stderr" -Level ERROR
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
                Write-Log "Command failed. Retrying in $backoff seconds... ($lastError)" -Level WARN

                Start-Sleep -Seconds $backoff
            }
        }
    }

    if (-not $success -and -not $IgnoreError) {
        Write-Log "Command failed after $MaxRetries attempts: $cmdLine" -Level ERROR
        throw $lastError
    }
}

function Get-CommonEnvVars {
    # Returns common environment variables formatted for AWS Lambda --environment "Variables={...}"
    # Includes: OpenTelemetry, FeatureFlags, AWS SDK defaults
    return "OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4318,OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf,FeatureFlags__UnleashApiUrl=http://unleash:4242/api/,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test"
}

# -----------------------------------------------------------------------------
# High-Level Resource Management Functions (Idempotent)
# -----------------------------------------------------------------------------

function New-SnsTopic {
    param([string]$Name)
    Write-Log "Ensuring SNS Topic exists: $Name" -Level INFO
    $topic = Invoke-Aws -Service "sns" -Command "create-topic" -Arguments @("--name", $Name) -JsonOutput $true
    return $topic.TopicArn
}

function New-SqsQueue {
    param([string]$Name)
    Write-Log "Ensuring SQS Queue exists: $Name" -Level INFO
    $queue = Invoke-Aws -Service "sqs" -Command "create-queue" -Arguments @("--queue-name", $Name) -JsonOutput $true
    return $queue.QueueUrl
}

function New-SnsSubscription {
    param([string]$TopicArn, [string]$Protocol, [string]$Endpoint, [hashtable]$Attributes = @{})
    Write-Log "Ensuring SNS Subscription: $Protocol -> $Endpoint" -Level INFO
    
    $args = @("--topic-arn", $TopicArn, "--protocol", $Protocol, "--notification-endpoint", $Endpoint)
    if ($Attributes.Count -gt 0) {
        $attrList = @()
        foreach ($key in $Attributes.Keys) {
            $attrList += "$key=$($Attributes[$key])"
        }
        $args += "--attributes"
        $args += ($attrList -join ",")
    }
    
    Invoke-Aws -Service "sns" -Command "subscribe" -Arguments $args | Out-Null
}

function New-LambdaFunction {
    param(
        [string]$Name,
        [string]$Handler,
        [string]$RoleArn,
        [string]$ZipPath,
        [string]$Runtime = "dotnet8",
        [string]$Timeout = "30",
        [string]$MemorySize = "1024",
        [string]$EnvironmentVariables # Comma-separated Key=Value string
    )
    
    # Check if function exists
    $exists = Invoke-Aws -Service "lambda" -Command "get-function" -Arguments @("--function-name", $Name) -IgnoreError $true -JsonOutput $true
    
    if ($exists) {
        Write-Log "Updating existing Lambda function: $Name" -Level INFO
        Invoke-Aws -Service "lambda" -Command "update-function-code" -Arguments @("--function-name", $Name, "--zip-file", "fileb://$ZipPath") | Out-Null
        
        # Update config only if needed (simplified: always update)
        Invoke-Aws -Service "lambda" -Command "update-function-configuration" -Arguments @(
            "--function-name", $Name,
            "--handler", $Handler,
            "--timeout", $Timeout,
            "--memory-size", $MemorySize,
            "--environment", "Variables={$EnvironmentVariables}"
        ) | Out-Null
    } else {
        Write-Log "Creating new Lambda function: $Name" -Level INFO
        Invoke-Aws -Service "lambda" -Command "create-function" -Arguments @(
            "--function-name", $Name,
            "--runtime", $Runtime,
            "--handler", $Handler,
            "--role", $RoleArn,
            "--zip-file", "fileb://$ZipPath",
            "--timeout", $Timeout,
            "--memory-size", $MemorySize,
            "--environment", "Variables={$EnvironmentVariables}"
        ) | Out-Null
    }
}

function Grant-LambdaPermission {
    param([string]$FunctionName, [string]$StatementId, [string]$Principal, [string]$SourceArn)
    
    Write-Log "Ensuring Lambda Permission: $StatementId" -Level INFO
    
    # Check if permission exists (get-policy)
    $policy = Invoke-Aws -Service "lambda" -Command "get-policy" -Arguments @("--function-name", $FunctionName) -IgnoreError $true -JsonOutput $true
    
    # Simple check: if policy contains StatementId. A robust parser would parse JSON, but regex is faster for now.
    if ($policy -and ($policy.Policy -match $StatementId)) {
        Write-Log "Permission $StatementId already exists for $FunctionName" -Level INFO
    } else {
        Invoke-Aws -Service "lambda" -Command "add-permission" -Arguments @(
            "--function-name", $FunctionName,
            "--statement-id", $StatementId,
            "--action", "lambda:InvokeFunction",
            "--principal", $Principal,
            "--source-arn", $SourceArn
        ) | Out-Null
    }
}

function New-EventSourceMapping {
    param([string]$FunctionName, [string]$EventSourceArn, [int]$BatchSize = 10)
    
    Write-Log "Ensuring Event Source Mapping: $FunctionName <- $EventSourceArn" -Level INFO
    
    # List mappings
    $mappings = Invoke-Aws -Service "lambda" -Command "list-event-source-mappings" -Arguments @("--function-name", $FunctionName, "--event-source-arn", $EventSourceArn) -JsonOutput $true
    
    if ($mappings -and $mappings.EventSourceMappings.Count -gt 0) {
        Write-Log "Event source mapping already exists for $FunctionName" -Level INFO
    } else {
        Invoke-Aws -Service "lambda" -Command "create-event-source-mapping" -Arguments @(
            "--function-name", $FunctionName,
            "--event-source-arn", $EventSourceArn,
            "--batch-size", "$BatchSize"
        ) | Out-Null
    }
}

function Remove-SnsTopic {
    param([string]$TopicArn)
    Remove-AwsResource -Description "SNS Topic ($TopicArn)" -Action {
        Invoke-Aws -Service "sns" -Command "delete-topic" -Arguments @("--topic-arn", $TopicArn) -IgnoreError $true
    }
}

function Remove-SqsQueue {
    param([string]$QueueUrl)
    Remove-AwsResource -Description "SQS Queue ($QueueUrl)" -Action {
        Invoke-Aws -Service "sqs" -Command "delete-queue" -Arguments @("--queue-url", $QueueUrl) -IgnoreError $true
    }
}

function Remove-LambdaFunction {
    param([string]$FunctionName)
    
    # 1. Delete Event Source Mappings (triggers) first to avoid dependency errors
    Write-Log "Checking Event Source Mappings for $FunctionName..." -Level INFO
    $mappings = Invoke-Aws -Service "lambda" -Command "list-event-source-mappings" -Arguments @("--function-name", $FunctionName) -JsonOutput $true -IgnoreError $true
    
    if ($mappings -and $mappings.EventSourceMappings) {
        foreach ($mapping in $mappings.EventSourceMappings) {
            Remove-AwsResource -Description "Event Source Mapping ($($mapping.UUID))" -Action {
                Invoke-Aws -Service "lambda" -Command "delete-event-source-mapping" -Arguments @("--uuid", $mapping.UUID) -IgnoreError $true
            }
        }
    }

    # 2. Delete the function
    Remove-AwsResource -Description "Lambda Function ($FunctionName)" -Action {
        Invoke-Aws -Service "lambda" -Command "delete-function" -Arguments @("--function-name", $FunctionName) -IgnoreError $true
    }
}

function New-DynamoDbTable {
    param(
        [hashtable]$TableDef
    )
    
    $TableName = $TableDef.TableName
    Write-Log "Ensuring DynamoDB Table exists: $TableName" -Level INFO
    
    # Check if table exists
    $table = Invoke-Aws -Service "dynamodb" -Command "describe-table" -Arguments @("--table-name", $TableName) -IgnoreError $true -JsonOutput $true
    
    if ($table -and $table.Table) {
        Write-Log "DynamoDB Table '$TableName' already exists." -Level INFO
        return $table.Table.TableArn
    } else {
        Write-Log "Creating DynamoDB Table: $TableName" -Level INFO
        
        # Convert definition to JSON for CLI
        $TableJson = $TableDef | ConvertTo-Json -Depth 10
        $TableJsonFile = Join-Path "$env:TEMP" "dynamodb_table_$($TableName)_$($PID).json"
        $TableJson | Set-Content -Path $TableJsonFile -Encoding Ascii
        
        Invoke-Aws -Service "dynamodb" -Command "create-table" -Arguments @("--cli-input-json", "file://$TableJsonFile") | Out-Null
        
        Remove-Item $TableJsonFile -ErrorAction SilentlyContinue
        
        return "arn:aws:dynamodb:us-east-1:000000000000:table/$TableName"
    }
}

function Remove-DynamoDbTable {
    param([string]$TableName)
    Remove-AwsResource -Description "DynamoDB Table ($TableName)" -Action {
        Invoke-Aws -Service "dynamodb" -Command "delete-table" -Arguments @("--table-name", $TableName) -IgnoreError $true
    }
}

function Invoke-LambdaFunction {
    param([string]$FunctionName, [string]$PayloadFile, [string]$OutputFile)
    
    Write-Log "Invoking Lambda: $FunctionName" -Level INFO
    
    # We need to run this differently because 'aws lambda invoke' writes to a file, which Invoke-Aws doesn't natively return.
    # However, Invoke-Aws is just a wrapper around Start-Process 'aws'.
    # We can use Invoke-Aws to run the command, and it will handle the process execution.
    # We just need to pass the arguments correctly.
    
    Invoke-Aws -Service "lambda" -Command "invoke" -Arguments @(
        "--function-name", $FunctionName,
        "--payload", "fileb://$PayloadFile",
        $OutputFile
    ) | Out-Null
    
    if (-not (Test-Path $OutputFile)) {
        throw "Lambda invocation failed to create output file: $OutputFile"
    }
}

function New-ApiGateway {
    param(
        [string]$Name,
        [string]$CustomId = $null
    )
    
    Write-Log "Ensuring API Gateway exists: $Name" -Level INFO
    
    # 1. If CustomId is provided, check if it exists by ID first (LocalStack optimization)
    if ($CustomId) {
        Write-Log "Checking for API Gateway with Static ID: $CustomId" -Level INFO
        $apiById = Invoke-Aws -Service "apigateway" -Command "get-rest-api" -Arguments @("--rest-api-id", $CustomId) -IgnoreError $true -JsonOutput $true
        
        if ($apiById -and $apiById.id -eq $CustomId) {
            Write-Log "Found existing API Gateway by ID: $CustomId" -Level INFO
            return $CustomId
        }
    }

    # 2. Fallback: List APIs to find by Name
    $apis = Invoke-Aws -Service "apigateway" -Command "get-rest-apis" -JsonOutput $true
    
    $api = $null
    if ($apis -and $apis.items) {
        $api = $apis.items | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    }
    
    if ($api) {
        Write-Log "API Gateway '$Name' already exists (ID: $($api.id))." -Level INFO
        return $api.id
    } else {
        Write-Log "Creating API Gateway: $Name" -Level INFO
        
        $args = @("--name", $Name)
        
        # LocalStack specific: Use tags to enforce a custom ID
        if ($CustomId) {
            Write-Log "Using LocalStack Custom ID: $CustomId" -Level INFO
            $args += "--tags"
            $args += "_custom_id_=$CustomId"
        }
        
        $newApi = Invoke-Aws -Service "apigateway" -Command "create-rest-api" -Arguments $args -JsonOutput $true
        return $newApi.id
    }
}



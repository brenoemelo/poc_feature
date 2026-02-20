# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy" -Level INFO
. "$PSScriptRoot/config.local.ps1"

$ZipPath = "$PSScriptRoot/../../../$($ServiceConfig.Name).zip"

# Create Table
Write-Log "Creating DynamoDB Table: $($ServiceConfig.DynamoTable)..." -Level INFO
$TableDef = @{
    TableName = $ServiceConfig.DynamoTable
    AttributeDefinitions = @(
        @{ AttributeName = "material_id"; AttributeType = "S" },
        @{ AttributeName = "record_type"; AttributeType = "S" }
    )
    KeySchema = @(
        @{ AttributeName = "material_id"; KeyType = "HASH" }
    )
    ProvisionedThroughput = @{
        ReadCapacityUnits = 5
        WriteCapacityUnits = 5
    }
    GlobalSecondaryIndexes = @(
        @{
            IndexName = "IX_Materials_By_Type"
            KeySchema = @(
                @{ AttributeName = "record_type"; KeyType = "HASH" },
                @{ AttributeName = "material_id"; KeyType = "RANGE" }
            )
            Projection = @{ ProjectionType = "ALL" }
            ProvisionedThroughput = @{
                ReadCapacityUnits = 5
                WriteCapacityUnits = 5
            }
        }
    )
}

# Convert to JSON for AWS CLI to avoid parsing issues
    $TableJson = $TableDef | ConvertTo-Json -Depth 4
    $TableJsonFile = Join-Path "$PSScriptRoot/../../tmp" "materials-table.json"
    $TableJson | Set-Content -Path $TableJsonFile -Encoding Ascii

    Write-Log "DEBUG: JSON Path: $TableJsonFile" -Level INFO
    # Use fileb:// for binary/file handling to avoid encoding issues or file:// with quotes
    aws dynamodb create-table --cli-input-json "file://$TableJsonFile" --endpoint-url http://localhost:4566 --no-cli-pager | Out-Null
if ($LASTEXITCODE -ne 0) { throw "DynamoDB Table Creation Failed" }

# Create SNS Topic
Write-Log "Creating SNS Topic: $($ServiceConfig.SnsTopic)" -Level INFO
aws sns create-topic --name $($ServiceConfig.SnsTopic) --endpoint-url http://localhost:4566 --no-cli-pager | Out-Null
if ($LASTEXITCODE -ne 0) { throw "SNS Topic Creation Failed" }

# Create Lambda
Write-Log "Creating Lambda Function..." -Level INFO
aws lambda create-function --function-name $($ServiceConfig.Name) --runtime dotnet10 --handler PoC.Materials --role arn:aws:iam::000000000000:role/lambda-role --zip-file fileb://$ZipPath --environment "Variables={Materials__TableName=$($ServiceConfig.DynamoTable),Otel__Endpoint=http://otel-collector:4318,FeatureFlags__UnleashApiUrl=http://unleash:4242/api/,AWS__Region=us-east-1}" --endpoint-url http://localhost:4566 --timeout 30 --memory-size 1024 --no-cli-pager | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Lambda Function Creation Failed" }

# Add API Gateway Permission
Write-Log "Adding API Gateway Permission..." -Level INFO
aws lambda add-permission --function-name $($ServiceConfig.Name) --statement-id apigateway-invoke --action lambda:InvokeFunction --principal apigateway.amazonaws.com --source-arn "arn:aws:execute-api:us-east-1:000000000000:$($Global:Config.ApiGateway.Id)/*/*/*" --endpoint-url http://localhost:4566 --no-cli-pager | Out-Null
if ($LASTEXITCODE -ne 0) { throw "API Gateway Permission Failed" }

Write-Log "Deployment Successful." -Level SUCCESS

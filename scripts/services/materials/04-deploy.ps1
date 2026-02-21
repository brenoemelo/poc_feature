# 04-deploy.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
. "$PSScriptRoot/../../utils/aws_helpers.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 4: Deploy" -Level INFO
. "$PSScriptRoot/config.local.ps1"

$ZipPath = "$PSScriptRoot/../../../PoC-Materials.zip"

if (-not (Test-Path $ZipPath)) {
    throw "Build artifact not found: $ZipPath"
}

$AbsZipPath = Resolve-Path $ZipPath
Write-Log "Absolute ZIP path resolved to: $AbsZipPath" -Level INFO

Write-Log "Deploying AWS Resources..." -Level INFO

# 1. DynamoDB Table
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
New-DynamoDbTable -TableDef $TableDef | Out-Null

# 2. SNS Topic
$TopicArn = New-SnsTopic -Name $($ServiceConfig.SnsTopic)

# 3. Lambda Function
$EnvVars = "Materials__TableName=$($ServiceConfig.DynamoTable),$(Get-CommonEnvVars)"
New-LambdaFunction -Name $($ServiceConfig.Name) `
    -Handler "PoC.Materials" `
    -RoleArn "arn:aws:iam::000000000000:role/lambda-role" `
    -ZipPath $AbsZipPath `
    -Timeout "30" `
    -MemorySize "1024" `
    -EnvironmentVariables $EnvVars

# 4. API Gateway Permission
Grant-LambdaPermission -FunctionName $($ServiceConfig.Name) `
    -StatementId "apigateway-invoke" `
    -Principal "apigateway.amazonaws.com" `
    -SourceArn "arn:aws:execute-api:us-east-1:000000000000:$($Global:Config.ApiGateway.Id)/*/*/*"

Write-Log "Deployment Successful." -Level SUCCESS

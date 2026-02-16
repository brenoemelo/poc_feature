
$ErrorActionPreference = "Stop"
Start-Transcript -Path "test_price_ingestion.log" -Append

$EndpointUrl = "http://localhost:4566"
$Region = "us-east-1"
$TopicArn = "arn:aws:sns:us-east-1:000000000000:material-events"
$TableName = "costing-prices-table"

# 1. Generate Test Data
$ComponentName = "TestComponent-$(Get-Random)"
$UnitPrice = 42.99
$Currency = "USD"
$Event = @{
    EventId = [Guid]::NewGuid().ToString()
    Timestamp = (Get-Date).ToUniversalTime().ToString("o")
    ComponentName = $ComponentName
    UnitPrice = $UnitPrice
    Currency = $Currency
    UpdatedAt = (Get-Date).ToUniversalTime().ToString("o")
}
$MessageBody = $Event | ConvertTo-Json

# 2. Publish to SNS
Write-Host "Publishing event to $TopicArn..."

# Construct the command for cmd.exe to avoid PowerShell quote stripping issues
$Cmd = "aws sns publish --topic-arn $TopicArn --message ""$($MessageBody.Replace('"', '\"'))"" --message-attributes ""{\""EventType\"":{\""DataType\"":\""String\"",\""StringValue\"":\""PriceUpdated\""}}"" --endpoint-url $EndpointUrl --region $Region --no-sign-request"
Write-Host "Executing: $Cmd"

$ErrorActionPreference = "Continue"
# Fix for time skew: Use local time to match LocalStack
# $env:TZ = "UTC" 
cmd /c $Cmd > publish_output.txt 2>&1
# $env:TZ = "" # Reset
$ErrorActionPreference = "Stop"

if ($LASTEXITCODE -ne 0) { 
    Write-Host "Command failed:"
    Get-Content publish_output.txt
    throw "Failed to publish message" 
}
Get-Content publish_output.txt

# 3. Wait for Processing
Write-Host "Waiting for processing..."
Start-Sleep -Seconds 10

# 4. Verify DynamoDB
Write-Host "Verifying DynamoDB table $TableName..."
$Result = aws dynamodb get-item --table-name $TableName --key "{""ComponentName"":{""S"":""$ComponentName""}}" --endpoint-url $EndpointUrl --region $Region | ConvertFrom-Json

if ($Result.Item) {
    Write-Host "SUCCESS: Item found!" -ForegroundColor Green
    Write-Host ($Result.Item | ConvertTo-Json -Depth 5)
} else {
    Write-Host "FAILURE: Item not found." -ForegroundColor Red
    
    # Debug: Check Queue
    Write-Host "Checking Queue Attributes..."
    aws sqs get-queue-attributes --queue-url "$EndpointUrl/000000000000/costing-ingestion-queue" --attribute-names All --endpoint-url $EndpointUrl --region $Region
    
    # Debug: Check Lambda Logs
    Write-Host "Checking Lambda Logs..."
    aws logs tail "/aws/lambda/PoC-Costing-PriceIngestion" --endpoint-url $EndpointUrl --region $Region
}

Stop-Transcript

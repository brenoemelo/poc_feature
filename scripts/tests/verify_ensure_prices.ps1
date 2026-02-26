$ErrorActionPreference = "Stop"

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
$EndpointUrl = if ($Global:Config) { $Global:Config.Aws.LocalStackUrl } else { "http://localhost:4566" }
$AccountId = if ($Global:Config) { $Global:Config.Aws.AccountId } else { "000000000000" }
$Region = if ($Global:Config) { $Global:Config.Aws.Region } else { "us-east-1" }

$TraceId = "0af7651916cd43dd8448eb211c80319c"
$SpanId = "b7ad6b7169203331"
$TraceParent = "00-$TraceId-$SpanId-01"

Write-Host "1. Populating some materials first..."
$MaterialsBody = '{\"target\":\"materials\",\"batch_size\":5}'
aws sns publish `
    --endpoint-url $EndpointUrl `
    --topic-arn arn:aws:sns:$Region:$AccountId:population-requests `
    --message $MaterialsBody `
    --message-attributes "traceparent={DataType=String,StringValue=$TraceParent}"

Start-Sleep -Seconds 5

Write-Host "2. Triggering ensure-prices job..."
$EnsurePricesBody = '{\"target\":\"ensure-prices\",\"batch_size\":1}'
aws sns publish `
    --endpoint-url $EndpointUrl `
    --topic-arn arn:aws:sns:$Region:$AccountId:population-requests `
    --message $EnsurePricesBody `
    --message-attributes "traceparent={DataType=String,StringValue=$TraceParent}"

Start-Sleep -Seconds 10

Write-Host "3. Checking logs..."
# Get the latest log stream
$LogStreamName = aws logs describe-log-streams `
    --log-group-name /aws/lambda/PoC-Populator-Worker `
    --endpoint-url $EndpointUrl `
    --order-by LastEventTime `
    --descending `
    --limit 1 `
    --query "logStreams[0].logStreamName" `
    --output text

if (-not $LogStreamName -or $LogStreamName -eq "None") {
    Write-Host "No log streams found."
    exit 1
}

Write-Host "Latest Log Stream: $LogStreamName"

# Get log events
aws logs get-log-events `
    --log-group-name /aws/lambda/PoC-Populator-Worker `
    --log-stream-name $LogStreamName `
    --endpoint-url $EndpointUrl `
    --output text

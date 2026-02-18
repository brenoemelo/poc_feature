$TraceId = "0af7651916cd43dd8448eb211c80319c"
$SpanId = "b7ad6b7169203331"
$TraceParent = "00-$TraceId-$SpanId-01"

Write-Host "Publishing message with traceparent: $TraceParent"

$MessageAttributes = "traceparent={DataType=String,StringValue=$TraceParent}"

$MessageBody = '{\"target\":\"materials\",\"batch_size\":1}'

aws sns publish `
    --endpoint-url http://localhost:4566 `
    --topic-arn arn:aws:sns:us-east-1:000000000000:population-requests `
    --message $MessageBody `
    --message-attributes $MessageAttributes

Start-Sleep -Seconds 5

Write-Host "Checking logs for TraceId: $TraceId"

# Get the latest log stream
$LogStreamName = aws logs describe-log-streams `
    --log-group-name /aws/lambda/PoC-Populator-Worker `
    --endpoint-url http://localhost:4566 `
    --order-by LastEventTime `
    --descending `
    --limit 1 `
    --query "logStreams[0].logStreamName" `
    --output text

if (-not $LogStreamName) {
    Write-Host "No log streams found."
    exit 1
}

Write-Host "Latest Log Stream: $LogStreamName"

# Get log events
aws logs get-log-events `
    --log-group-name /aws/lambda/PoC-Populator-Worker `
    --log-stream-name $LogStreamName `
    --endpoint-url http://localhost:4566 `
    --output text

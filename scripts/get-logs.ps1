
$logGroup = "/aws/lambda/PoC-Materials"
$stream = aws --endpoint-url http://localhost:4566 logs describe-log-streams --log-group-name $logGroup --order-by LastEventTime --descending --limit 1 | ConvertFrom-Json
$streamName = $stream.logStreams[0].logStreamName
Write-Host "Fetching logs from stream: $streamName"
aws --endpoint-url http://localhost:4566 logs get-log-events --log-group-name $logGroup --log-stream-name $streamName --limit 50

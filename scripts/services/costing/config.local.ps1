# Costing Service Configuration
# Load Global Config
. "$PSScriptRoot/../../config/global.env.ps1"

$ServiceConfig = @{
    Name = "PoC-Costing"
    ProjectPath = "$PSScriptRoot/../../../src/PoC.Costing/PoC.Costing.csproj"
    DynamoTable = "costing-prices-table"
    
    # Ingestion Function & Queue
    IngestionFunctionName = "PoC-Costing-PriceIngestion"
    IngestionQueueName = "costing-ingestion-queue"
    IngestionQueueArn = "arn:aws:sqs:us-east-1:000000000000:costing-ingestion-queue"
    
    # Dependencies
    MaterialsTopicArn = "arn:aws:sns:us-east-1:000000000000:material-events"
    MaterialsApiUrl = "http://localstack:4566/_aws/execute-api/$($Global:Config.ApiGateway.Id)/prod/"
}

# Costing Service Configuration
# Load Global Config
. "$PSScriptRoot/../../config/global.env.ps1"

$ServiceConfig = @{
    Name = $Global:Resources.Lambda.Costing.ApiFunction
    ProjectPath = "$PSScriptRoot/../../../src/PoC.Costing/PoC.Costing.csproj"
    DynamoTable = $Global:Resources.DynamoDb.CostingTable
    
    # Ingestion Function & Queue
    IngestionFunctionName = $Global:Resources.Lambda.Costing.IngestionFunction
    IngestionQueueName = $Global:Resources.Sqs.CostingIngestionQueue
    IngestionQueueArn = "arn:aws:sqs:us-east-1:000000000000:$($Global:Resources.Sqs.CostingIngestionQueue)"
    
    # Dependencies
    MaterialsTopicArn = "arn:aws:sns:us-east-1:000000000000:$($Global:Resources.Sns.MaterialEventsTopic)"
    MaterialsApiUrl = "http://localstack:4566/_aws/execute-api/$($Global:Config.ApiGateway.Id)/prod/"
}

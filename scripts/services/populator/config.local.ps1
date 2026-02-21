# Populator Service Configuration
# Load Global Config
. "$PSScriptRoot/../../config/global.env.ps1"

$ServiceConfig = @{
    Name = $Global:Resources.Lambda.Populator.ApiFunction
    ProjectPath = "$PSScriptRoot/../../../src/PoC.Populator/PoC.Populator.csproj"
    
    # Worker Function
    WorkerName = $Global:Resources.Lambda.Populator.WorkerFunction
    
    # DynamoDB Tables
    MaterialsTableName = $Global:Resources.DynamoDb.MaterialsTable
    PricesTableName = $Global:Resources.DynamoDb.CostingTable

    # SNS/SQS
    TopicName = $Global:Resources.Sns.PopulationRequestsTopic
    TopicArn = "arn:aws:sns:us-east-1:000000000000:$($Global:Resources.Sns.PopulationRequestsTopic)"
    OutputTopicName = $Global:Resources.Sns.MaterialEventsTopic
    OutputTopicArn = "arn:aws:sns:us-east-1:000000000000:$($Global:Resources.Sns.MaterialEventsTopic)"
    QueueName = $Global:Resources.Sqs.PopulatorQueue
    QueueArn = "arn:aws:sqs:us-east-1:000000000000:$($Global:Resources.Sqs.PopulatorQueue)"
    
    # Dependencies
    MaterialsApiUrl = "http://localstack:4566/_aws/execute-api/$($Global:Config.ApiGateway.Id)/prod/"
}

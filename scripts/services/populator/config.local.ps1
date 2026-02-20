# Populator Service Configuration
# Load Global Config
. "$PSScriptRoot/../../config/global.env.ps1"

$ServiceConfig = @{
    Name = "PoC-Populator"
    ProjectPath = "$PSScriptRoot/../../../src/PoC.Populator/PoC.Populator.csproj"
    
    # Worker Function
    WorkerName = "PoC-Populator-Worker"
    
    # SNS/SQS
    TopicName = "population-requests"
    TopicArn = "arn:aws:sns:us-east-1:000000000000:population-requests"
    QueueName = "populator-queue"
    QueueArn = "arn:aws:sqs:us-east-1:000000000000:populator-queue"
    
    # Dependencies
    MaterialsApiUrl = "http://localstack:4566/restapis/material-api/prod/_user_request_/"
}

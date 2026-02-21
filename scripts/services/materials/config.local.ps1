# Materials Service Configuration
# Load Global Config
. "$PSScriptRoot/../../config/global.env.ps1"

$ServiceConfig = @{
    Name = $Global:Resources.Lambda.Materials.ApiFunction
    IngestionFunctionName = $Global:Resources.Lambda.Materials.IngestionFunction
    IngestionQueueName = $Global:Resources.Sqs.MaterialIngestionQueue
    ProjectPath = "$PSScriptRoot/../../../src/PoC.Materials/PoC.Materials.csproj"
    DynamoTable = $Global:Resources.DynamoDb.MaterialsTable
    SnsTopic = $Global:Resources.Sns.MaterialEventsTopic
}

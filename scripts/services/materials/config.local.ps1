# Materials Service Configuration
# Load Global Config
. "$PSScriptRoot/../../config/global.env.ps1"

$ServiceConfig = @{
    Name = "PoC-Materials"
    ProjectPath = "$PSScriptRoot/../../../src/PoC.Materials/PoC.Materials.csproj"
    DynamoTable = "materials-table"
    SnsTopic = "material-events"
}

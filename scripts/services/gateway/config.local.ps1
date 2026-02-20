# config.local.ps1
# API Gateway Configuration

# Load Global Config
. "$PSScriptRoot/../../config/global.env.ps1"

# Service Specific Config
$ServiceConfig = @{
    Name = "PoC-Gateway"
    ApiName = "Material-Formulation-API"
    ApiId = $Global:Config.ApiGateway.Id
    Stage = $Global:Config.ApiGateway.Stage
    Region = $Global:Config.Aws.Region
    EndpointUrl = $Global:Config.Aws.LocalStackUrl
    OpenApiPath = Resolve-Path "$PSScriptRoot/../../../docs/openapi.yaml"
}

# Export to Global Scope
$Global:ServiceConfig = $ServiceConfig

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }

if (-not $Global:Config) {
    throw "Global Configuration not loaded. Please ensure global.env.ps1 is available."
}

$EndpointUrl = $Global:Config.Aws.LocalStackUrl
$Region = $Global:Config.Aws.Region

aws dynamodb list-tables --endpoint-url $EndpointUrl --region $Region

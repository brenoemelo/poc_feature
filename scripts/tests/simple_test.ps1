# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
$EndpointUrl = if ($Global:Config) { $Global:Config.Aws.LocalStackUrl } else { "http://localhost:4566" }
$Region = if ($Global:Config) { $Global:Config.Aws.Region } else { "us-east-1" }

aws dynamodb list-tables --endpoint-url $EndpointUrl --region $Region

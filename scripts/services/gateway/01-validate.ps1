# 01-validate.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 1: Validation" -Level INFO

# Check Tools
Assert-Command "aws"

# Check AWS Connection
# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
$EndpointUrl = if ($Global:Config) { $Global:Config.Aws.LocalStackUrl } else { "http://localhost:4566" }

Assert-AwsConnection -EndpointUrl $EndpointUrl

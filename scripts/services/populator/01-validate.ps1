# 01-validate.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 1: Validation" -Level INFO

# Check Tools
Assert-Command "dotnet"
Assert-Command "docker"
Assert-Command "aws"

# Check AWS Connection
Assert-AwsConnection -EndpointUrl "http://localhost:4566"

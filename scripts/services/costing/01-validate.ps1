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
aws sts get-caller-identity --endpoint-url http://localhost:4566 --no-cli-pager | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Log "AWS Connection (LocalStack) Verified." -Level SUCCESS
} else {
    Write-Log "Failed to connect to LocalStack." -Level ERROR
    exit 1
}

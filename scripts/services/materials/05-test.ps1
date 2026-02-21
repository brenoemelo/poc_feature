# 05-test.ps1
param([string]$LogFile)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 5: Smoke Tests" -Level INFO
. "$PSScriptRoot/config.local.ps1"

# Invoke Lambda directly
$Result = aws lambda invoke --function-name $($ServiceConfig.Name) --endpoint-url http://localhost:4566 --cli-connect-timeout 10 --cli-read-timeout 60 --payload '{}' response.json --no-cli-pager

if ($LASTEXITCODE -eq 0) {
    Write-Log "Smoke Test Passed: Lambda Invocation Successful." -Level SUCCESS
} else {
    Write-Log "Smoke Test Failed." -Level ERROR
    exit 1
}

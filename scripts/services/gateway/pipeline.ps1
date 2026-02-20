# pipeline.ps1
param([switch]$SkipBuild)
$ErrorActionPreference = "Stop"

# Load Shared Utils
. "$PSScriptRoot/../../utils/logger.ps1"

# Init Logger
$LogFile = Init-Log -ServiceName "Gateway"

Write-Log ">>> STARTING PIPELINE: PoC.Gateway <<<" -Level INFO

# 2. Execute Stages
try {
    & "$PSScriptRoot/01-validate.ps1" -LogFile $LogFile
    & "$PSScriptRoot/02-cleanup.ps1" -LogFile $LogFile
    # No Build Stage for Gateway
    & "$PSScriptRoot/04-deploy.ps1" -LogFile $LogFile
    & "$PSScriptRoot/05-test.ps1" -LogFile $LogFile
    
    Write-Log ">>> PIPELINE COMPLETED SUCCESSFULLY <<<" -Level SUCCESS
} catch {
    Write-Log ">>> PIPELINE FAILED <<<" -Level ERROR
    Write-Log "$_" -Level ERROR
    exit 1
} finally {
    Stop-Transcript -ErrorAction SilentlyContinue
}

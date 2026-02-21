param([switch]$SkipBuild)
$ErrorActionPreference = "Stop"

# Load Shared Utils
. "$PSScriptRoot/../../utils/logger.ps1"

# Init Logger
$LogFile = Init-Log -ServiceName "populator"

Write-Log ">>> STARTING PIPELINE: PoC.Populator <<<" -Level INFO

# 2. Execute Stages
try {
    & "$PSScriptRoot/01-validate.ps1" -LogFile $LogFile
    & "$PSScriptRoot/02-cleanup.ps1" -LogFile $LogFile
    if (-not $SkipBuild) {
        & "$PSScriptRoot/03-build.ps1" -LogFile $LogFile
    } else {
        Write-Log "Build Stage Skipped." -Level WARN
    }
    & "$PSScriptRoot/04-deploy.ps1" -LogFile $LogFile
    #& "$PSScriptRoot/05-test.ps1" -LogFile $LogFile
    
    Write-Log ">>> PIPELINE COMPLETED SUCCESSFULLY <<<" -Level SUCCESS
    exit 0
} catch {
    Write-Log ">>> PIPELINE FAILED <<<" -Level ERROR
    Write-Log $_ -Level ERROR
    exit 1
} finally {
    Stop-Transcript -ErrorAction SilentlyContinue
}

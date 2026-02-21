param([switch]$SkipBuild)
$ErrorActionPreference = "Stop"

# Load Shared Utils
. "$PSScriptRoot/../../utils/logger.ps1"

# Init Logger
$LogFile = Init-Log -ServiceName "populator"

Write-Log ">>> STARTING PIPELINE: PoC.Populator <<<" -Level INFO

# Load Configuration to get Service Name
. "$PSScriptRoot/config.local.ps1"

# Prepare Artifact Path
$ArtifactsDir = Resolve-Path "$PSScriptRoot/../../../deployment/artifacts"
$ServiceName = $ServiceConfig.Name

if (-not $SkipBuild) {
    $Timestamp = Get-Date -Format "yyyyMMddHHmmss"
    $ArtifactPath = "$ArtifactsDir\$ServiceName`_$Timestamp.zip"
    Write-Log "Target Artifact: $ArtifactPath" -Level INFO
} else {
    # Find latest artifact if skipping build
    $LatestArtifact = Get-ChildItem -Path "$ArtifactsDir" -Filter "$ServiceName`_*.zip" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $LatestArtifact) {
        Write-Log "No existing artifact found in $ArtifactsDir to deploy." -Level ERROR
        exit 1
    }
    $ArtifactPath = $LatestArtifact.FullName
    Write-Log "Skipping Build. Using latest artifact: $ArtifactPath" -Level INFO
}

# 2. Execute Stages
try {
    & "$PSScriptRoot/01-validate.ps1" -LogFile $LogFile
    & "$PSScriptRoot/02-cleanup.ps1" -LogFile $LogFile
    if (-not $SkipBuild) {
        & "$PSScriptRoot/03-build.ps1" -LogFile $LogFile -ArtifactPath $ArtifactPath
    } else {
        Write-Log "Build Stage Skipped." -Level WARN
    }
    & "$PSScriptRoot/04-deploy.ps1" -LogFile $LogFile -ArtifactPath $ArtifactPath
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

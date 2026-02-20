# -----------------------------------------------------------------------------
# Master Deployment Script (Modular Framework)
# Orchestrates the deployment of all microservices using isolated pipelines.
# -----------------------------------------------------------------------------

param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# 1. Load Shared Utilities
$ScriptsRoot = Resolve-Path "$PSScriptRoot/../../scripts"
. "$ScriptsRoot/utils/logger.ps1"
. "$ScriptsRoot/utils/common.ps1"
. "$ScriptsRoot/utils/aws_helpers.ps1"

# 2. Initialize Logging
$LogFile = Init-Log -ServiceName "Master-Deploy"

Write-Log ">>> STARTING MASTER DEPLOYMENT <<<" -Level INFO
Write-Log "Parameters: SkipBuild=$SkipBuild" -Level INFO

# 3. Validation
try {
    Write-Log "STEP 1: Global Validation" -Level INFO
    Assert-AwsConnection
    
    # 4. Execute Service Pipelines
    # Order matters: Materials (Shared SNS) -> Costing/Populator -> Gateway
    
    # Define Services
    $Services = @(
        @{ Name = "PoC.Materials"; Path = "materials/pipeline.ps1" },
        @{ Name = "PoC.Costing";   Path = "costing/pipeline.ps1" },
        @{ Name = "PoC.Populator"; Path = "populator/pipeline.ps1" },
        @{ Name = "PoC.Gateway";   Path = "gateway/pipeline.ps1" }
    )

    foreach ($Service in $Services) {
        Write-Log "--------------------------------------------------" -Level INFO
        Write-Log "Deploying Service: $($Service.Name)" -Level INFO
        Write-Log "--------------------------------------------------" -Level INFO
        
        $PipelinePath = Join-Path "$ScriptsRoot/services" $Service.Path
        
        # Build Arguments
        $ArgsList = @("-ExecutionPolicy", "Bypass", "-File", "$PipelinePath")
        if ($SkipBuild) { $ArgsList += "-SkipBuild" }
        
        # Execute Pipeline in Isolated Process
        Write-Log "Executing: powershell $ArgsList" -Level INFO
        
        & powershell @ArgsList
        
        if ($LASTEXITCODE -ne 0) {
            throw "Pipeline for $($Service.Name) failed with exit code $LASTEXITCODE"
        }
    }

    Write-Log ">>> MASTER DEPLOYMENT COMPLETED SUCCESSFULLY <<<" -Level SUCCESS

} catch {
    Write-Log ">>> MASTER DEPLOYMENT FAILED <<<" -Level ERROR
    Write-Log "$_" -Level ERROR
    Write-Log $($_.Exception | Out-String) -Level ERROR
    exit 1
} finally {
    Stop-Transcript -ErrorAction SilentlyContinue
}

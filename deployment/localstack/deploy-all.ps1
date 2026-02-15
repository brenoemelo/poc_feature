
# -----------------------------------------------------------------------------
# Master Deployment Script
# Orchestrates the deployment of all microservices to LocalStack.
# -----------------------------------------------------------------------------

# Load shared utilities
. "$PSScriptRoot\utils.ps1"

# Initialize environment for master script logging
Initialize-Environment
Assert-AwsConnection
Assert-NotProduction

Write-Log "Starting Full Deployment for LocalStack..." "Success"

$ScriptDir = $PSScriptRoot

function Run-DeployScript {
    param($ScriptName)
    
    $ScriptPath = Join-Path $ScriptDir $ScriptName
    Write-Log "----------------------------------------------------------------" "Debug"
    Write-Log "Executing: $ScriptName" "Info"
    Write-Log "----------------------------------------------------------------" "Debug"
    
    try {
        & $ScriptPath
        if ($LASTEXITCODE -ne 0) {
            throw "Script $ScriptName exited with code $LASTEXITCODE"
        }
    }
    catch {
        Write-Log "Deployment failed at $ScriptName" "Error"
        Write-Log $_.Exception.Message "Error"
        exit 1
    }
}

# Execute in dependency order
Run-DeployScript "materials.ps1"
Run-DeployScript "costing.ps1"
Run-DeployScript "populator.ps1"
Run-DeployScript "gateway.ps1"

Write-Log "----------------------------------------------------------------" "Debug"
Write-Log "Full Deployment Completed Successfully!" "Success"
Write-Log "----------------------------------------------------------------" "Debug"

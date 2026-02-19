# -----------------------------------------------------------------------------
# Master Deployment Script
# Orchestrates the deployment of all microservices to LocalStack.
# -----------------------------------------------------------------------------

param(
    [switch]$SkipBuild
)

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
    # Check if $ScriptName has arguments (simple heuristic)
    if ($ScriptName -match " ") {
        # Split command and arguments
        $parts = $ScriptName -split " ", 2
        $file = Join-Path $ScriptDir $parts[0]
        $argsList = $parts[1]
        $ScriptPath = "$file $argsList"
    }

    Write-Log "----------------------------------------------------------------" "Debug"
    Write-Log "Executing: $ScriptName" "Info"
    Write-Log "----------------------------------------------------------------" "Debug"
    
    # Initialize LASTEXITCODE to avoid "not defined" errors in some environments
    $global:LASTEXITCODE = 0
    
    try {
        Invoke-Expression "& $ScriptPath"
        if ($global:LASTEXITCODE -ne 0) {
            throw "Script $ScriptName exited with code $global:LASTEXITCODE"
        }
    }
    catch {
        Write-Log "Deployment failed at $ScriptName" "Error"
        Write-Log $_.Exception.Message "Error"
        exit 1
    }
}

# 2. Execute in dependency order with SkipBuild
# 2. Execute in dependency order
# Materials must go first to set up shared SNS topics
$BuildFlag = if ($SkipBuild) { "-SkipBuild" } else { "" }
Run-DeployScript "materials.ps1 $BuildFlag"

# 3. Compile and Deploy Remaining Services (Sequential)
Write-Log "Starting Sequential Deployment for Costing and Populator..." "Info"

Run-DeployScript "costing.ps1 $BuildFlag"
Run-DeployScript "populator.ps1 $BuildFlag"
Run-DeployScript "gateway.ps1"

Write-Log "----------------------------------------------------------------" "Debug"
Write-Log "Full Deployment Completed Successfully!" "Success"
Write-Log "----------------------------------------------------------------" "Debug"

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
# Materials must go first to set up shared SNS topics
Run-DeployScript "materials.ps1 -SkipBuild"

Write-Log "Starting Parallel Deployment for Costing and Populator..." "Info"

# Run Costing and Populator in parallel using Start-Process
$costingPath = Join-Path $ScriptDir "costing.ps1"
$populatorPath = Join-Path $ScriptDir "populator.ps1"

$p1 = Start-Process -FilePath "powershell" -ArgumentList "-ExecutionPolicy", "Bypass", "-File", $costingPath, "-SkipBuild" -PassThru -NoNewWindow
$p2 = Start-Process -FilePath "powershell" -ArgumentList "-ExecutionPolicy", "Bypass", "-File", $populatorPath, "-SkipBuild" -PassThru -NoNewWindow

# Wait for both
$procs = @($p1, $p2)
$procs | Wait-Process

# Check Exit Codes
$failed = $false
foreach ($p in $procs) {
    if ($p.ExitCode -ne 0) {
        Write-Log "Process $($p.Id) failed with exit code $($p.ExitCode)." "Error"
        $failed = $true
    }
}

if ($failed) {
    Write-Log "Parallel deployment failed." "Error"
    exit 1
}

# Gateway depends on all Lambdas
Run-DeployScript "gateway.ps1"

Write-Log "----------------------------------------------------------------" "Debug"
Write-Log "Full Deployment Completed Successfully!" "Success"
Write-Log "----------------------------------------------------------------" "Debug"

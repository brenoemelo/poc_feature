# Common.ps1
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Load Logger
. "$PSScriptRoot/logger.ps1"

function Assert-Command {
    param([string]$Command)
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        Write-Log "CRITICAL: Required command '$Command' not found." -Level ERROR
        exit 1
    }
}

function Assert-EnvVar {
    param([string]$Name)
    if ([string]::IsNullOrWhiteSpace((Get-Item env:$Name -ErrorAction SilentlyContinue).Value)) {
        Write-Log "CRITICAL: Environment variable '$Name' is missing." -Level ERROR
        exit 1
    }
}

# Trap to catch unhandled errors
trap {
    Write-Log "FATAL ERROR at line $($_.InvocationInfo.ScriptLineNumber): $($_.Exception.Message)" -Level ERROR
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 1
}

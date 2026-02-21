# Logger.ps1
param(
    [string]$LogFile
)

function Write-Log {
    param(
        [Parameter(Mandatory=$true)][string]$Message,
        [ValidateSet("INFO", "WARN", "ERROR", "SUCCESS")][string]$Level = "INFO"
    )

    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $FormattedMsg = "[$Timestamp] [$Level] $Message"
    
    # Console Color
    $Color = switch ($Level) {
        "INFO"    { "Cyan" }
        "WARN"    { "Yellow" }
        "ERROR"   { "Red" }
        "SUCCESS" { "Green" }
    }

    # Output to Console (Transcript will capture this)
    Write-Host $FormattedMsg -ForegroundColor $Color
}

$LoggerScriptRoot = $PSScriptRoot

function Init-Log {
    param(
        [string]$ServiceName,
        [switch]$CleanAll
    )
    
    $LogDir = Join-Path $LoggerScriptRoot "../logs"
    if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
    
    # Clean logs if requested
    if ($CleanAll) {
        Write-Host "Cleaning all logs in $LogDir..." -ForegroundColor Gray
        $LogFiles = Get-ChildItem -Path $LogDir -Filter "*.log"
        foreach ($File in $LogFiles) {
            try {
                Remove-Item $File.FullName -Force -ErrorAction Stop
            } catch {
                Write-Warning "Could not delete log file: $($File.Name). It might be in use."
            }
        }
    } else {
        # Auto-clean logs older than 7 days (fallback)
        Get-ChildItem -Path $LogDir -Filter "*.log" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } | Remove-Item -Force
    }

    # Create new log file path
    $Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $Global:CurrentLogFile = Join-Path $LogDir "${ServiceName}_pipeline_${Timestamp}.log"
    
    Write-Host "Logging to: $Global:CurrentLogFile" -ForegroundColor Gray
    
    # Start Transcript to capture ALL output (stdout, stderr, Write-Host)
    Start-Transcript -Path $Global:CurrentLogFile -Append -Force
    
    return $Global:CurrentLogFile
}

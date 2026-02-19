
$ErrorActionPreference = "Stop"

function Check-Status {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:4566/restapis/material-api/prod/_user_request_/api/v1/materials" -Method Get -UseBasicParsing -ErrorAction Stop
        Write-Host "Endpoint Status: $($response.StatusCode)"
    } catch {
        if ($_.Exception.Response) {
            Write-Host "Endpoint Status: $($_.Exception.Response.StatusCode)"
        } else {
            Write-Host "Endpoint Error: $($_.Exception.Message)"
        }
    }
}

Write-Host "--- Verifying Feature Gate Integration ---"

# 1. Disable
Write-Host "1. Disabling 'materials-crud'..."
powershell -ExecutionPolicy Bypass -File scripts/toggle-flag.ps1 -State off
Write-Host "Waiting 15 seconds for propagation..."
Start-Sleep -Seconds 15
Check-Status

# 2. Enable
Write-Host "`n2. Enabling 'materials-crud'..."
powershell -ExecutionPolicy Bypass -File scripts/toggle-flag.ps1 -State on
Write-Host "Waiting 15 seconds for propagation..."
Start-Sleep -Seconds 15
Check-Status

Write-Host "`n--- Verification Complete ---"

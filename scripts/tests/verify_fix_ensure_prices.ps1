
$ErrorActionPreference = "Stop"

$PopulatorUrl = "http://localhost:4566/restapis/material-api/prod/_user_request_/api/v1/populator/jobs"

Write-Host "Triggering Ensure Prices..."
$Payload = @{
    target = "ensure-prices"
    count  = 1
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri $PopulatorUrl -Method POST -Body $Payload -ContentType "application/json"
    Write-Host "Job submitted successfully."
}
catch {
    Write-Error "Failed to submit job: $_"
}

$StartTimeUnix = [DateTimeOffset]::UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds()
Write-Host "Start Time (Unix ms): $StartTimeUnix"

Write-Host "Checking Lambda Logs (polling for 30s)..."

for ($i = 0; $i -lt 6; $i++) {
    Start-Sleep -Seconds 5
    $LogEvents = aws --endpoint-url=http://localhost:4566 logs filter-log-events --log-group-name /aws/lambda/PoC-Populator-Worker --start-time $StartTimeUnix --limit 20 --interleaved --output json | ConvertFrom-Json
    
    if ($LogEvents.events) {
        break
    }
    Write-Host "No logs yet..."
}

$FoundError = $false
$FoundSuccess = $false

if ($LogEvents.events) {
    foreach ($e in $LogEvents.events) {
        if ($e.message -match "404") {
            Write-Host "FOUND 404 ERROR: $($e.message)" -ForegroundColor Red
            $FoundError = $true
        }
        if ($e.message -match "Published .* events for target ensure-prices") {
            Write-Host "FOUND SUCCESS: $($e.message)" -ForegroundColor Green
            $FoundSuccess = $true
        }
        # Also check for 200 OK from Materials Client
        if ($e.message -match "Received HTTP response .* 200") {
            Write-Host "FOUND API 200 OK: $($e.message)" -ForegroundColor Green
        }
    }
}

if ($FoundError) {
    Write-Error "Verification FAILED: 404 Error found in logs."
}
elseif ($FoundSuccess) {
    Write-Host "Verification PASSED: Success message found in logs." -ForegroundColor Green
}
else {
    Write-Warning "Verification INCONCLUSIVE: Neither success nor 404 found. Check logs manually."
}

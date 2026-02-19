
$ApiUrl = "http://localhost:4242/api"
$AdminToken = "*:*.admin-token"
$Project = "default"
$Env = "development"
$Feature = "materials-crud"

function Set-Flag($state) {
    $uri = "$ApiUrl/admin/projects/$Project/features/$Feature/environments/$Env/$state"
    Invoke-RestMethod -Uri $uri -Headers @{Authorization=$AdminToken; "Content-Type"="application/json"} -Method Post -Body "{}" | Out-Null
    Write-Host "Flag '$Feature' set to $state"
}

function Check-Endpoint {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:4566/restapis/material-api/prod/_user_request_/api/v1/materials" -Method Get -ErrorAction Stop
        Write-Host "Endpoint returned: $($response.StatusCode)"
    } catch {
        if ($_.Exception.Response) {
             Write-Host "Endpoint returned: $($_.Exception.Response.StatusCode)"
        } else {
             Write-Host "Endpoint error: $($_.Exception.Message)"
        }
    }
}

Write-Host "--- Test Start ---"

# 1. Disable flag
Set-Flag "off"
Write-Host "Waiting 5 seconds..."
Start-Sleep -Seconds 5
Check-Endpoint

# 2. Enable flag
Set-Flag "on"
Write-Host "Waiting 5 seconds..."
Start-Sleep -Seconds 5
Check-Endpoint

Write-Host "--- Test End ---"

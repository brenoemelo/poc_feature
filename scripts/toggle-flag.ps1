
param([string]$State)

$ApiUrl = "http://localhost:4242/api"
$AdminToken = "*:*.admin-token"
$Project = "default"
$Env = "development"
$Feature = "materials-crud"

if ($State -ne "on" -and $State -ne "off") {
    Write-Host "Usage: toggle-flag.ps1 -State [on|off]"
    exit 1
}

$uri = "$ApiUrl/admin/projects/$Project/features/$Feature/environments/$Env/$State"
try {
    Invoke-RestMethod -Uri $uri -Headers @{Authorization=$AdminToken; "Content-Type"="application/json"} -Method Post -Body "{}" | Out-Null
    Write-Host "Flag '$Feature' set to $State"
} catch {
    Write-Host "Error setting flag: $_"
    exit 1
}

$ErrorActionPreference = "Stop"

$ApiUrl = "http://localhost:4242/api"
$AdminToken = "*:*.admin-token"
$ClientToken = "*:development.unleash-insecure-api-token"
$AppName = "poc-app"
$InstanceId = "unleash-init-ps"

Write-Host "Starting Unleash initialization..."

# 1. Wait for Unleash to be ready
Write-Host "Waiting for Unleash API at $ApiUrl..."
$retryCount = 0
$maxRetries = 12 # 1 minute
while ($retryCount -lt $maxRetries) {
    try {
        # Try to list features to check connectivity and auth
        $response = Invoke-RestMethod -Uri "$ApiUrl/admin/projects/default/features" -Headers @{Authorization=$AdminToken} -Method Get -ErrorAction Stop
        Write-Host "Unleash is up!"
        break
    }
    catch {
        $err = $_.Exception.Message
        if ($_.Exception.Response) {
             $statusCode = $_.Exception.Response.StatusCode.value__
             Write-Host "Unleash responded with $statusCode. Retrying..."
             # If 401/403, maybe token is wrong but service is up? But we need admin access.
        } else {
             Write-Host "Unleash is not ready yet ($err). Retrying in 5 seconds..."
        }
        Start-Sleep -Seconds 5
        $retryCount++
    }
}

if ($retryCount -eq $maxRetries) {
    Write-Host "Unleash failed to start."
    exit 1
}

# 2. Create Features
Write-Host "Creating Features..."
$features = @("price-calculation", "count-all-prices", "view-all-prices", "price-ingestion", "materials-crud", "costing-batch", "population-jobs", "view-all-components")

foreach ($feature in $features) {
    # Check if feature exists
    try {
        $exists = Invoke-RestMethod -Uri "$ApiUrl/admin/projects/default/features/$feature" -Headers @{Authorization=$AdminToken} -Method Get -ErrorAction SilentlyContinue
    } catch {
        $exists = $null
    }

    if ($null -eq $exists) {
        Write-Host "Creating feature '$feature'..."
        $body = @{
            name = $feature
            description = "Feature created by init script"
            type = "release"
            project = "default"
            enabled = $true
            strategies = @(
                @{ name = "default" }
            )
        } | ConvertTo-Json -Depth 5

        Invoke-RestMethod -Uri "$ApiUrl/admin/projects/default/features" -Headers @{Authorization=$AdminToken; "Content-Type"="application/json"} -Method Post -Body $body | Out-Null
        
        # Enable for environment 'development'
        Write-Host "Enabling '$feature' for development..."
        $envBody = @{ enabled = $true } | ConvertTo-Json
        Invoke-RestMethod -Uri "$ApiUrl/admin/projects/default/features/$feature/environments/development/on" -Headers @{Authorization=$AdminToken; "Content-Type"="application/json"} -Method Post -Body $envBody | Out-Null
    } else {
        Write-Host "Feature '$feature' already exists."
        
        # Ensure it is enabled for development even if it exists
         Write-Host "Ensuring '$feature' is enabled for development..."
        $envBody = @{ enabled = $true } | ConvertTo-Json
        try {
             Invoke-RestMethod -Uri "$ApiUrl/admin/projects/default/features/$feature/environments/development/on" -Headers @{Authorization=$AdminToken; "Content-Type"="application/json"} -Method Post -Body $envBody | Out-Null
        } catch {
            # Ignore if already enabled or other minor error
        }
    }
}

# 3. Create Local Admin User (Handled by UNLEASH_DEFAULT_ADMIN_PASSWORD in docker-compose)
Write-Host "Ensure you can login with user 'admin' and password 'password'."

Write-Host "Unleash initialization complete."

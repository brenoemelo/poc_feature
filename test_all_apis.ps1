$ErrorActionPreference = "Continue"

# 1. Try to load from environment file
$EnvFile = Join-Path $PSScriptRoot ".env.local"
if (Test-Path $EnvFile) {
    Get-Content $EnvFile | ForEach-Object {
        if ($_ -match "([^=]+)=(.*)") {
            Set-Variable -Name $matches[1] -Value $matches[2] -Scope Script
        }
    }
}

# 2. Determine Base URL with Fallback
$BaseUrl = $null

function Test-ApiUrl {
    param($Url)
    try {
        $testPath = "$Url/api/v1/materials" # Use a known safe GET path
        $response = Invoke-RestMethod -Uri $testPath -Method GET -ErrorAction Stop
        return $true
    }
    catch {
        # 404 means service reachable but path/resource not found (which might be okay if just testing base URL connectivity, but here we expect /materials to exist)
        # However, for LocalStack Custom Domain 404, it means the routing failed.
        return $false
    }
}

if ($API_FIXED_URL) {
    Write-Host "Checking Fixed URL: $API_FIXED_URL ..." -NoNewline
    if (Test-ApiUrl $API_FIXED_URL) {
        $BaseUrl = $API_FIXED_URL
        Write-Host " OK" -ForegroundColor Green
    }
    else {
        Write-Host " Unreachable/404 (Skipping)" -ForegroundColor Red
    }
}

if (-not $BaseUrl) {
    if ($API_GATEWAY_ID) {
        $ApiId = $API_GATEWAY_ID
        $DynamicUrl = "http://localhost:4566/restapis/$ApiId/prod/_user_request_"
        Write-Host "Using API ID from .env.local: $ApiId" -ForegroundColor Cyan
        $BaseUrl = $DynamicUrl
    }
    else {
        # Default fallback
        $ApiId = "material-api"
        $BaseUrl = "http://localhost:4566/restapis/$ApiId/prod/_user_request_"
        Write-Host "Using Hardcoded API ID: $ApiId" -ForegroundColor Yellow
    }
}

Write-Host "Using API Gateway Base URL: $BaseUrl" -ForegroundColor Cyan

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Body = $null
    )
    
    $Uri = "$BaseUrl$Path"
    Write-Host "[$Method] $Uri" -NoNewline
    
    try {
        $params = @{
            Method      = $Method
            Uri         = $Uri
            ContentType = "application/json"
        }
        if ($Body) {
            $params.Body = $Body
        }
        
        $response = Invoke-RestMethod @params
        Write-Host " - OK" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host " - FAILED" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response Body: $responseBody" -ForegroundColor Gray
        }
        return $null
    }
}

Write-Host "`n--- Testing PoC.Materials ---" -ForegroundColor Yellow
# 1. Create Material
$materialId = "mat-" + (Get-Random)
$material = @{
    material_id = $materialId
    name        = "Test Material $materialId"
    formulation = @(
        @{ component = "Polycarbonate"; percentage = 80.0; type = "Polymer" }
        @{ component = "CarbonFiber"; percentage = 20.0; type = "Reinforcement" }
    )
} | ConvertTo-Json -Depth 5

$created = Invoke-Api -Method POST -Path "/api/v1/materials" -Body $material

# 2. Get All Materials
$all = Invoke-Api -Method GET -Path "/api/v1/materials"
if ($all -and $all.items -and $all.items.Count -ge 0) { Write-Host "Found $($all.items.Count) materials (Page 1)." }

# 3. Get Specific Material
if ($created) {
    Invoke-Api -Method GET -Path "/api/v1/materials/$materialId" | Out-Null
}

Write-Host "`n--- Testing PoC.Costing ---" -ForegroundColor Yellow
# 1. Upsert Price 1
$price1 = @{
    component_name = "Polycarbonate"
    unit_price     = 5.50
    unit           = "kg"
    currency       = "USD"
} | ConvertTo-Json

Invoke-Api -Method POST -Path "/api/v1/costing/prices" -Body $price1 | Out-Null

# 2. Upsert Price 2
$price2 = @{
    component_name = "CarbonFiber"
    unit_price     = 25.00
    unit           = "kg"
    currency       = "USD"
} | ConvertTo-Json

Invoke-Api -Method POST -Path "/api/v1/costing/prices" -Body $price2 | Out-Null

# 3. Calculate Cost
$calcReq = @{
    material_id = $materialId
    formulation = @(
        @{ component = "Polycarbonate"; percentage = 80.0 }
        @{ component = "CarbonFiber"; percentage = 20.0 }
    )
} | ConvertTo-Json

Invoke-Api -Method POST -Path "/api/v1/costing/estimations" -Body $calcReq | Out-Null


Write-Host "`n--- Testing PoC.Populator ---" -ForegroundColor Yellow
# 1. Trigger Population
$popReq = @{
    target = "materials"
    count  = 10
} | ConvertTo-Json

Invoke-Api -Method POST -Path "/api/v1/populator/jobs" -Body $popReq | Out-Null


# 4. Calculate Batch Cost
Write-Host "Testing Batch Cost..." -NoNewline
$batch = Invoke-Api -Method GET -Path "/api/v1/costing/estimations/batch"
if ($batch -and $batch.Count -ge 0) {
    Write-Host " OK - Calculated costs for $($batch.Count) materials" -ForegroundColor Green
}
else {
    Write-Host " FAILED or Empty" -ForegroundColor Red
}

Write-Host "`n--- Tests Completed ---" -ForegroundColor Green



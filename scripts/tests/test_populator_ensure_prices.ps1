$ErrorActionPreference = "Stop"

# Load environment
$EnvFile = Join-Path $PSScriptRoot ".env.local"
if (Test-Path $EnvFile) {
    Get-Content $EnvFile | ForEach-Object {
        if ($_ -match "([^=]+)=(.*)") {
            Set-Variable -Name $matches[1] -Value $matches[2] -Scope Script
        }
    }
}

$ApiId = "material-api"
if (-not [string]::IsNullOrWhiteSpace($API_GATEWAY_ID)) { 
    $ApiId = $API_GATEWAY_ID 
}
$BaseUrl = "http://localhost:4566/restapis/$ApiId/prod/_user_request_"
$PopulatorUrl = "$BaseUrl/api/v1/populator/jobs"
$MaterialsCountUrl = "$BaseUrl/api/v1/materials/count"
$MaterialsUrl = "$BaseUrl/api/v1/materials"

Write-Host "Using Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "Materials URL: $MaterialsUrl" -ForegroundColor Cyan

# 1. Check Material Count
Write-Host "Checking Material Count..."
try {
    $CountResponse = Invoke-RestMethod -Uri $MaterialsCountUrl -Method GET
    $Count = $CountResponse.count
    Write-Host "Current Material Count: $Count" -ForegroundColor Cyan
} catch {
    Write-Host "Failed to get count. Assuming 0." -ForegroundColor Yellow
    $Count = 0
}

# 2. Populate Materials if needed
if ($Count -lt 10) {
    Write-Host "Populating Materials (Target=10, Min=3, Max=5)..."
    $Payload = @{
        target = "materials"
        count = 10
        minComponents = 3
        maxComponents = 5
    } | ConvertTo-Json

    Invoke-RestMethod -Uri $PopulatorUrl -Method POST -Body $Payload -ContentType "application/json"
    
    # Wait for population
    Write-Host "Waiting for materials to be populated..."
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        try {
            $CountResponse = Invoke-RestMethod -Uri $MaterialsCountUrl -Method GET
            $NewCount = $CountResponse.count
            Write-Host "Current Count: $NewCount"
            if ($NewCount -ge 10) { break }
        } catch { }
    }
}

# 3. Verify Components Range (Validation of Requirement)
Write-Host "Verifying Component Counts..."
$Uri = "$MaterialsUrl?limit=10"
Write-Host "GET $Uri"
try {
    if ([string]::IsNullOrWhiteSpace($MaterialsUrl) -or $MaterialsUrl -match "restapis//") {
        throw "Invalid Materials URL: $MaterialsUrl"
    }

    $Response = Invoke-RestMethod -Uri $Uri -Method GET
    $Materials = $Response.data
    foreach ($m in $Materials) {
        $CompCount = $m.formulation.Count
        if ($CompCount -ge 3 -and $CompCount -le 5) {
            Write-Host "Material $($m.material_id) has $CompCount components (Pass)" -ForegroundColor Green
        } else {
            Write-Host "Material $($m.material_id) has $CompCount components (FAIL)" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "Failed to fetch materials: $_" -ForegroundColor Red
}

# 4. Trigger Ensure Prices
Write-Host "Triggering Ensure Prices..."
$Payload = @{
    target = "ensure-prices"
    count = 1 # Count is ignored but required by validator
} | ConvertTo-Json

Invoke-RestMethod -Uri $PopulatorUrl -Method POST -Body $Payload -ContentType "application/json"

# 5. Monitor Price Ingestion
Write-Host "Monitoring Price Ingestion (checking DynamoDB directly)..."
$TableName = "costing-prices-table"
$EndpointUrl = "http://localhost:4566"
$Region = "us-east-1"

for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    $Result = aws dynamodb scan --table-name $TableName --select COUNT --endpoint-url $EndpointUrl --region $Region | ConvertFrom-Json
    $PriceCount = $Result.Count
    Write-Host "Price Count: $PriceCount"
    if ($PriceCount -gt 0) { 
        Write-Host "SUCCESS: Prices found in DynamoDB!" -ForegroundColor Green
        break 
    }
}

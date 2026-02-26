
$ErrorActionPreference = "Stop"

function Test-Endpoint {
    param (
        [string]$Url,
        [string]$Method = "GET",
        [string]$Body = $null,
        [string]$Description
    )

    Write-Host "Testing $Description..." -NoNewline
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            UseBasicParsing = $true
            ContentType = "application/json"
        }
        if ($Body) {
            $params.Body = $Body
        }

        $response = Invoke-WebRequest @params
        Write-Host " OK ($($response.StatusCode))" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host " FAILED ($($_))" -ForegroundColor Red
        if ($_.Exception.Response) {
            Write-Host "Response Body: " -NoNewline
            $reader = New-Object System.IO.StreamReader $_.Exception.Response.GetResponseStream()
            Write-Host $reader.ReadToEnd()
        }
        return $null
    }
}

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
$baseUrl = if ($Global:Config) { 
    if ($Global:Config.ApiGateway.UrlTemplate) {
        $Global:Config.ApiGateway.UrlTemplate.Replace("{api_id}", $Global:Config.ApiGateway.Id).Replace("{stage}", $Global:Config.ApiGateway.Stage)
    } else {
        $Global:Config.Aws.LocalStackUrl + "/_aws/execute-api/" + $Global:Config.ApiGateway.Id + "/" + $Global:Config.ApiGateway.Stage
    }
} else { 
    "http://localhost:4566/_aws/execute-api/material-api/prod" 
}

if ($baseUrl.EndsWith("/")) { $baseUrl = $baseUrl.TrimEnd("/") }

# 1. Check Materials API
Test-Endpoint -Url "$baseUrl/api/v1/materials" -Description "Materials API Health"

# 2. Check Costing API
Test-Endpoint -Url "$baseUrl/api/v1/costing/prices" -Description "Costing API Health"

# 3. Add Prices
$ironPrice = @{
    component_name = "Iron"
    unit_price = 10.0
    unit = "kg"
    currency = "USD"
} | ConvertTo-Json

$carbonPrice = @{
    component_name = "Carbon"
    unit_price = 5.0
    unit = "kg"
    currency = "USD"
} | ConvertTo-Json

Test-Endpoint -Url "$baseUrl/api/v1/costing/prices" -Method "POST" -Body $ironPrice -Description "Add Price: Iron"
Test-Endpoint -Url "$baseUrl/api/v1/costing/prices" -Method "POST" -Body $carbonPrice -Description "Add Price: Carbon"

# 4. Create Material
$materialId = "MAT-TEST-001"
$material = @{
    material_id = $materialId
    name = "Super Alloy Test"
    density = @{ value = 7.8; unit = "g/cm3" }
    formulation = @(
        @{ component = "Iron"; percentage = 80; type = "Metal" },
        @{ component = "Carbon"; percentage = 20; type = "Non-Metal" }
    )
    properties = @{ hardness = "high" }
} | ConvertTo-Json -Depth 5

Test-Endpoint -Url "$baseUrl/api/v1/materials" -Method "POST" -Body $material -Description "Create Material: Super Alloy"

# 5. Calculate Cost
$calculation = @{
    material_id = $materialId
    formulation = @(
        @{ component = "Iron"; percentage = 80 },
        @{ component = "Carbon"; percentage = 20 }
    )
    desired_margin_percent = 25.0
} | ConvertTo-Json -Depth 5

$response = Test-Endpoint -Url "$baseUrl/api/v1/costing/estimations" -Method "POST" -Body $calculation -Description "Calculate Cost"

if ($response) {
    $content = $response.Content | ConvertFrom-Json
    Write-Host "Calculation Result:" -ForegroundColor Cyan
    Write-Host ($content | ConvertTo-Json -Depth 5)
    
    # Check if total cost is correct (9.0)
    if ($content.data.total_cost -eq 9.0) {
        Write-Host "VERIFICATION SUCCESS: Total Cost is 9.0" -ForegroundColor Green
    } else {
        Write-Host "VERIFICATION FAILED: Expected 9.0, got $($content.data.total_cost)" -ForegroundColor Red
    }
}

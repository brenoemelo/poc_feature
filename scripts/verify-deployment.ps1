$ErrorActionPreference = "Stop"

function Get-FunctionUrl {
    param($FunctionName)
    try {
        $config = aws --endpoint-url=http://localhost:4566 lambda get-function-url-config --function-name $FunctionName | ConvertFrom-Json
        return $config.FunctionUrl
    }
    catch {
        Write-Host "Error getting URL for $FunctionName" -ForegroundColor Red
        return $null
    }
}

function Invoke-Test {
    param($Url, $Method, $Path, $Body = $null)
    $FullUrl = "$($Url.TrimEnd('/'))$Path"
    Write-Host "[$Method] $FullUrl" -NoNewline
    
    try {
        $params = @{
            Method      = $Method
            Uri         = $FullUrl
            ContentType = "application/json"
        }
        if ($Body) { $params.Body = $Body }
        
        $response = Invoke-RestMethod @params
        Write-Host " - OK" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host " - FAILED" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            Write-Host "Body: $($reader.ReadToEnd())" -ForegroundColor Gray
        }
        return $null
    }
}

Write-Host "--- Verifying Deployment (Function URLs) ---" -ForegroundColor Cyan

# 1. Materials
$MatUrl = Get-FunctionUrl "PoC-Materials"
if ($MatUrl) {
    Write-Host "Materials URL: $MatUrl" -ForegroundColor Yellow
    
    # Create
    $matBody = @{
        material_id = "verif-" + (Get-Random)
        name        = "Verification Material"
        record_type = "MATERIAL"
        formulation = @(
            @{ component = "Polymer"; percentage = 100 }
        )
    } | ConvertTo-Json
    
    Invoke-Test -Url $MatUrl -Method POST -Path "/api/v1/materials" -Body $matBody
    
    # Get All
    Invoke-Test -Url $MatUrl -Method GET -Path "/api/v1/materials"
}

# 2. Costing
$CostUrl = Get-FunctionUrl "PoC-Costing"
if ($CostUrl) {
    Write-Host "Costing URL: $CostUrl" -ForegroundColor Yellow
    
    # Upsert Price
    $priceBody = @{
        component_name = "Polymer"
        unit_price     = 10.0
        unit           = "kg"
        currency       = "USD"
    } | ConvertTo-Json
    Invoke-Test -Url $CostUrl -Method POST -Path "/api/v1/costing/prices" -Body $priceBody
    
    # Get Prices
    Invoke-Test -Url $CostUrl -Method GET -Path "/api/v1/costing/prices"
}

# 3. Populator
$PopUrl = Get-FunctionUrl "PoC-Populator"
if ($PopUrl) {
    Write-Host "Populator URL: $PopUrl" -ForegroundColor Yellow
    
    # Trigger Job
    $jobBody = @{ target = "materials"; count = 5 } | ConvertTo-Json
    Invoke-Test -Url $PopUrl -Method POST -Path "/api/v1/populator/jobs" -Body $jobBody
}

Write-Host "--- Verification Completed ---" -ForegroundColor Cyan

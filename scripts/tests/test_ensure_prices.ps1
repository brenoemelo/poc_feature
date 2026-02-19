
$ErrorActionPreference = "Continue"

# Setup Base URL
$BaseUrl = "http://localhost:4566/_aws/execute-api/material-api/prod"
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

# 1. Create some materials using existing Populator target 'materials'
Write-Host "`nCreating materials..." -ForegroundColor Cyan
$popReq = @{
    target = "materials"
    count  = 5
} | ConvertTo-Json

Invoke-Api -Method POST -Path "/api/v1/populator/jobs" -Body $popReq

# Wait for processing
Write-Host "Waiting 5 seconds for materials to be created..."
Start-Sleep -Seconds 5

# 2. Trigger 'ensure-prices'
Write-Host "`nTriggering ensure-prices..." -ForegroundColor Cyan
$priceReq = @{
    target = "ensure-prices"
    count  = 1
} | ConvertTo-Json

Invoke-Api -Method POST -Path "/api/v1/populator/jobs" -Body $priceReq

Write-Host "`nDone. Please check CloudWatch logs for PoC-Populator-Worker to verify." -ForegroundColor Green

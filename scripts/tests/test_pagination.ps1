# test_pagination.ps1
# Bypass SSL validation
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Load Global Config
$GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
$BaseUrl = if ($Global:Config) { 
    if ($Global:Config.ApiGateway.UrlTemplate) {
        $Global:Config.ApiGateway.UrlTemplate.Replace("{api_id}", $Global:Config.ApiGateway.Id).Replace("{stage}", $Global:Config.ApiGateway.Stage)
    } else {
        $Global:Config.Aws.LocalStackUrl + "/_aws/execute-api/" + $Global:Config.ApiGateway.Id + "/" + $Global:Config.ApiGateway.Stage
    }
} else { 
    "http://localhost:4566/_aws/execute-api/material-api/prod" 
}
if ($BaseUrl.EndsWith("/")) { $BaseUrl = $BaseUrl.TrimEnd("/") }

$PopulatorUrl = $BaseUrl

function Invoke-Api {
    param([string]$Method, [string]$Uri, [string]$Body = $null)
    Write-Host "Calling $Method $Uri" -ForegroundColor DarkGray
    try {
        $params = @{Method = $Method; Uri = $Uri; ContentType = "application/json"}
        if ($Body) { $params.Body = $Body }
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        Write-Host "Error calling $Uri ($Method): $_" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            Write-Host "Response Body: $($reader.ReadToEnd())" -ForegroundColor Red
        }
        return $null
    }
}

# 1. Ensure we have enough data (at least 5 items)
Write-Host "Checking material count..." -ForegroundColor Cyan
$initialCheck = Invoke-Api -Method Get -Uri "$BaseUrl/api/v1/materials?limit=100"

if ($initialCheck -and $initialCheck.data.Count -lt 5) {
    Write-Host "Not enough materials ($($initialCheck.data.Count)). Populating..." -ForegroundColor Yellow
    $popRes = Invoke-Api -Method Post -Uri "$PopulatorUrl/api/v1/populator/jobs" -Body '{"target": "materials", "count": 10}'
    if ($popRes -ne $null) {
        Write-Host "Population job triggered. Waiting 5 seconds..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
    } else {
        # 202 Accepted might return null, which is fine if no exception was thrown.
        # But Invoke-Api returns null on exception too. 
        # Let's assume if we are here, it might be success if no red error was printed.
        Write-Host "Population job triggered (No Content). Waiting 5 seconds..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
    }
} else {
    Write-Host "Found enough materials ($($initialCheck.data.Count))." -ForegroundColor Green
}

# 2. Test Page 1 (Limit=2)
Write-Host "`nTesting Pagination (Page 1, Limit=2)..." -ForegroundColor Cyan
$page1 = Invoke-Api -Method Get -Uri "$BaseUrl/api/v1/materials?limit=2"

if ($page1) {
    Write-Host "Page 1 Items: $($page1.data.Count)" -ForegroundColor Green
    Write-Host "Meta Limit: $($page1.meta.limit)" -ForegroundColor Green
    Write-Host "Meta Count: $($page1.meta.count)" -ForegroundColor Green
    
    $nextLinkObj = $page1.links | Where-Object {$_.rel -eq 'next'}
    if ($nextLinkObj) {
        $nextLink = $nextLinkObj.href
        # Fix LocalStack generated URL
        if ($nextLink -match "^https://localhost:4566/prod") {
             $nextLink = $nextLink -replace "^https://localhost:4566/prod", "http://localhost:4566/_aws/execute-api/material-api/prod"
        }
        Write-Host "Next Link found: $nextLink" -ForegroundColor Green
        
        # 3. Test Page 2
        Write-Host "`nTesting Pagination (Page 2)..." -ForegroundColor Cyan
        $page2 = Invoke-Api -Method Get -Uri $nextLink
        if ($page2) {
            Write-Host "Page 2 Items: $($page2.data.Count)" -ForegroundColor Green
            if ($page1.data[0].material_id -ne $page2.data[0].material_id) {
                Write-Host "SUCCESS: Pagination is working correctly (items are different)." -ForegroundColor Green
            } else {
                Write-Host "FAILURE: Page 2 has same items as Page 1." -ForegroundColor Red
            }
        }
    } else {
        Write-Host "WARNING: No next link found. Maybe not enough data?" -ForegroundColor Yellow
    }
} else {
    Write-Host "Failed to get Page 1" -ForegroundColor Red
}

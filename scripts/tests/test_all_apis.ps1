$ErrorActionPreference = "Continue"

# 0. Initialize Logging (Modular Framework)
try {
    . "$PSScriptRoot/../utils/logger.ps1"
    $LogFile = Init-Log -ServiceName "E2E_Tests"
} catch {
    Write-Warning "Logger not found or failed to initialize. Continuing without file logging."
}

# 1. Try to load from environment file
$EnvFile = Join-Path $PSScriptRoot "../../.env.local"
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

# If Fixed URL failed or not provided, try standard LocalStack localhost
if (-not $BaseUrl) {
    # 1.5 Load Global Config (Single Source of Truth)
    $GlobalConfigFile = "$PSScriptRoot/../config/global.env.ps1"
    if (Test-Path $GlobalConfigFile) { . $GlobalConfigFile }
    
    $LocalStackUrl = if ($Global:Config) { 
        if ($Global:Config.ApiGateway.UrlTemplate) {
            $Global:Config.ApiGateway.UrlTemplate.Replace("{api_id}", $Global:Config.ApiGateway.Id).Replace("{stage}", $Global:Config.ApiGateway.Stage)
        } else {
            $Global:Config.Aws.LocalStackUrl + "/_aws/execute-api/" + $Global:Config.ApiGateway.Id + "/" + $Global:Config.ApiGateway.Stage
        }
    } else { 
        throw "Global Configuration not loaded. Please ensure global.env.ps1 is available."
    }

    if ($LocalStackUrl.EndsWith("/")) { $LocalStackUrl = $LocalStackUrl.TrimEnd("/") }

    Write-Host "Checking LocalStack URL: $LocalStackUrl ..." -NoNewline
    # Simple check if LocalStack is up (not necessarily the API)
    try {
        $LocalStackHealthUrl = if ($Global:Config) { $Global:Config.Aws.LocalStackUrl + "/_localstack/health" } else { throw "Global Config not loaded" }
        $test = Invoke-WebRequest -Uri $LocalStackHealthUrl -Method GET -UseBasicParsing -ErrorAction SilentlyContinue
        if ($test.StatusCode -eq 200) {
             $BaseUrl = $LocalStackUrl
             Write-Host " LocalStack is UP (Assuming API is deployed)" -ForegroundColor Green
        } else {
             Write-Host " LocalStack Unreachable" -ForegroundColor Red
        }
    } catch {
         Write-Host " LocalStack Unreachable" -ForegroundColor Red
    }
}

if (-not $BaseUrl) {
    Write-Error "Could not determine API Base URL. Please ensure LocalStack is running or set API_FIXED_URL."
    exit 1
}

Write-Host "Running E2E Tests against: $BaseUrl" -ForegroundColor Cyan

# 3. Define Tests
$Tests = @(
    @{
        Name = "Create Material"
        Method = "POST"
        Path = "/api/v1/materials"
        Body = @{
            name = "Test Material $(Get-Date -Format 'yyyyMMddHHmmss')"
            cost = 10.5
        }
        ExpectedStatus = 201
    },
    @{
        Name = "Get Materials"
        Method = "GET"
        Path = "/api/v1/materials?limit=5"
        Body = $null
        ExpectedStatus = 200
    }
)

# 4. Execute Tests
$Failed = 0

foreach ($test in $Tests) {
    $Url = "$BaseUrl$($test.Path)"
    Write-Host "Test: $($test.Name) [$($test.Method) $Url]..." -NoNewline
    
    try {
        $params = @{
            Uri = $Url
            Method = $test.Method
            ContentType = "application/json"
            ErrorAction = "Stop"
        }
        
        if ($test.Body) {
            $params.Body = $test.Body | ConvertTo-Json
        }
        
        $response = Invoke-RestMethod @params
        
        # In PowerShell Core, Invoke-RestMethod returns the object directly for 2xx
        # We assume success if no exception was thrown
        Write-Host " PASS" -ForegroundColor Green
        
    } catch {
        $ex = $_.Exception
        if ($ex.Response) {
             $status = $ex.Response.StatusCode.value__
             if ($status -eq $test.ExpectedStatus) {
                 Write-Host " PASS (Expected $status)" -ForegroundColor Green
             } else {
                 Write-Host " FAIL (Status: $status)" -ForegroundColor Red
                 $Failed++
             }
        } else {
             Write-Host " FAIL (Error: $($ex.Message))" -ForegroundColor Red
             $Failed++
        }
    }
}

if ($Failed -gt 0) {
    Write-Error "$Failed tests failed."
    exit 1
} else {
    Write-Host "All tests passed." -ForegroundColor Green
    exit 0
}

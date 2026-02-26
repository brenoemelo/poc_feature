$ErrorActionPreference = "Stop"

# Configuration
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

$ApiUrl = "$BaseUrl/api/v1/materials?limit=1"
$TempoUrl = "http://localhost:3200/api/traces"

Write-Output "Starting debug_tempo.ps1"

# 1. Make API Request
Write-Output "Calling API: $ApiUrl"
try {
    $response = Invoke-WebRequest -Uri $ApiUrl -Method Get -UseBasicParsing
} catch {
    Write-Error "Failed to call API: $_"
    exit 1
}

# 2. Extract Trace ID
$traceId = $response.Headers["X-Trace-Id"]
if ([string]::IsNullOrWhiteSpace($traceId)) {
    Write-Error "X-Trace-Id header missing in response!"
    exit 1
}
Write-Output "Got TraceId: $traceId"

# 3. Poll Tempo
Write-Output "Polling Tempo for TraceId: $traceId (Timeout: 60s)"
$found = $false
for ($i = 0; $i -lt 60; $i++) {
    Write-Output "Iteration $i"
    try {
        $tempoUri = "$TempoUrl/$traceId"
        # Temporarily allow 404 without exception by using -SkipHttpErrorCheck if available in PS 7, but here we use try/catch
        # We can also use -ErrorAction SilentlyContinue for this specific call
        $tempoResponse = Invoke-WebRequest -Uri $tempoUri -Method Get -UseBasicParsing -ErrorAction Stop
        
        if ($tempoResponse.StatusCode -eq 200) {
            Write-Output "Trace found in Tempo!"
            $found = $true
            break
        }
    } catch {
        # Check if it's a 404
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode -eq [System.Net.HttpStatusCode]::NotFound) {
                    # Expected 404, continue
                } else {
                    Write-Output "Error polling Tempo: $($_.Exception.Message)"
                    if ($_.Exception.Response) {
                        Write-Output "Status: $($_.Exception.Response.StatusCode)"
                         $stream = $_.Exception.Response.GetResponseStream()
                         $reader = New-Object System.IO.StreamReader($stream)
                         $body = $reader.ReadToEnd()
                         Write-Output "Body: $body"
                    }
                }
            }
            Start-Sleep -Seconds 1
        }

if (-not $found) {
    Write-Error "Trace not found in Tempo after 60 seconds."
    exit 1
} else {
    Write-Output "Success! Trace propagated to Tempo."
}

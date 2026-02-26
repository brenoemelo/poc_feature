
$ErrorActionPreference = "Stop"

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

$Url = "$BaseUrl/api/v1/materials"

Write-Host "Calling GET $Url"
try {
    $response = Invoke-RestMethod -Uri $Url -Method GET -Verbose
    Write-Host "Response: $response"
} catch {
    Write-Host "Error: $_"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Host "Body: $($reader.ReadToEnd())"
    }
}

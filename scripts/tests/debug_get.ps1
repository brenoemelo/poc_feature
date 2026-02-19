
$ErrorActionPreference = "Stop"
$BaseUrl = "http://localhost:4566/restapis/material-api/prod/_user_request_"
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

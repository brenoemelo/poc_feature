
try {
    $response = Invoke-WebRequest -Uri "http://localhost:4566/restapis/material-api/prod/_user_request_/api/v1/materials" -Method Get -UseBasicParsing -ErrorAction Stop
    Write-Host "Status: $($response.StatusCode)"
} catch {
    if ($_.Exception.Response) {
        Write-Host "Status: $($_.Exception.Response.StatusCode)"
    } else {
        Write-Host "Error: $($_.Exception.Message)"
    }
}

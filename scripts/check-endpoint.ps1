
try {
    $response = Invoke-WebRequest -Uri "http://localhost:4566/_aws/execute-api/material-api/prod/api/v1/materials" -Method Get -UseBasicParsing -ErrorAction Stop
    Write-Host "Status: $($response.StatusCode)"
} catch {
    if ($_.Exception.Response) {
        Write-Host "Status: $($_.Exception.Response.StatusCode)"
    } else {
        Write-Host "Error: $($_.Exception.Message)"
    }
}

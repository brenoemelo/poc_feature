$ApiId = "bgl2ladyeo"
$Region = "us-east-1"
$BaseUrl = "http://localhost:4566/restapis/$ApiId/prod/_user_request_"

Write-Host "Testing APIs at $BaseUrl"
Write-Host "--------------------------------------------------"

function Test-Endpoint {
    param (
        [string]$Method,
        [string]$Path,
        [string]$Body = $null
    )

    $url = "$BaseUrl$Path"
    Write-Host "Testing $Method $Path..."

    try {
        $params = @{
            Uri = $url
            Method = $Method
            ErrorAction = "Stop"
        }

        if ($Body) {
            $params.Body = $Body
            $params.ContentType = "application/json"
        }

        $response = Invoke-RestMethod @params
        Write-Host "Success: " -NoNewline
        $response | ConvertTo-Json -Depth 5 | Write-Host
        Write-Host ""
    }
    catch {
        Write-Host "Failed: " -NoNewline
        Write-Host $_.Exception.Message -ForegroundColor Red
        if ($_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response Body: $responseBody" -ForegroundColor Red
        }
        Write-Host ""
    }
}

# 1. Populate (Populator)
Write-Host "1. Testing /populate (Populator)..."
$populateBody = @{
    count = 10
    target = "materials"
} | ConvertTo-Json
Test-Endpoint -Method "POST" -Path "/populate" -Body $populateBody

# 2. Update Price (Costing)
Write-Host "2. Testing /costing/prices (Costing - Upsert)..."
$costingBody = @{
    component_name = "MAT001"
    unit_price = 12.5
    unit = "kg"
    currency = "USD"
} | ConvertTo-Json
Test-Endpoint -Method "POST" -Path "/costing/prices" -Body $costingBody

# Cleanup: Delete existing material (if any) to ensure clean state
Write-Host "Cleaning up material MAT002..."
try {
    Invoke-RestMethod -Method "DELETE" -Uri "$BaseUrl/materials/MAT002" -ErrorAction SilentlyContinue
} catch {
    # Ignore errors during cleanup
}

# 3. Create Material (Materials)
Write-Host "3. Testing /materials (Materials - Create)..."
$materialBody = @{
    material_id = "MAT002"
    name = "Material 2"
    formulation = @(
        @{
            component = "MAT001"
            percentage = 100
            type = "primary"
        }
    )
} | ConvertTo-Json
Test-Endpoint -Method "POST" -Path "/materials" -Body $materialBody

# 4. Calculate Cost (Costing)
Write-Host "4. Testing /costing/calculate-cost (Costing)..."
$calculateBody = @{
    material_id = "MAT002"
    formulation = @(
        @{
            component = "MAT001"
            percentage = 100
            type = "primary"
        }
    )
} | ConvertTo-Json
Test-Endpoint -Method "POST" -Path "/costing/calculate-cost" -Body $calculateBody

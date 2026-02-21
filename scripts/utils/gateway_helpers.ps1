
function Get-Or-Create-ApiGateway {
    param(
        [string]$ApiName,
        [string]$EndpointUrl = "http://localhost:4566"
    )

    Write-Log "Checking for existing API Gateway: $ApiName" -Level INFO
    
    $ExistingApi = $null

    # List APIs
    $Apis = aws apigateway get-rest-apis --endpoint-url $EndpointUrl --no-cli-pager 2>&1 | ConvertFrom-Json
    
    if ($Apis.items) {
        $ExistingApi = $Apis.items | Where-Object { $_.name -eq $ApiName } | Select-Object -First 1
    }

    if ($ExistingApi) {
        Write-Log "Found existing API Gateway: $($ExistingApi.id)" -Level INFO
        return $ExistingApi.id
    }

    Write-Log "Creating new API Gateway: $ApiName" -Level INFO
    $NewApi = aws apigateway create-rest-api --name $ApiName --endpoint-url $EndpointUrl --no-cli-pager 2>&1 | ConvertFrom-Json
    
    if (-not $NewApi.id) {
        throw "Failed to create API Gateway"
    }

    Write-Log "Created API Gateway: $($NewApi.id)" -Level SUCCESS
    return $NewApi.id
}

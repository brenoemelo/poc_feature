
$filePath = "d:\Projetos\poc_feature\docs\api\insomnia_antigravity_v1.json"
$jsonContent = Get-Content -Path $filePath -Raw | ConvertFrom-Json

# Helper to find if request exists
function Test-RequestExists {
    param (
        [string]$Name,
        [string]$ParentId
    )
    $exists = $jsonContent.resources | Where-Object { $_._type -eq "request" -and $_.name -eq $Name -and $_.parentId -eq $ParentId }
    return $exists -ne $null
}

$newRequests = @()
$currentTime = [int64]((Get-Date).ToUniversalTime() - (Get-Date "1970-01-01").ToUniversalTime()).TotalMilliseconds

# 1. Get Unique Components (Materials)
if (-not (Test-RequestExists -Name "Get Unique Components" -ParentId "fld_materials")) {
    $newRequests += @{
        "_id" = "req_materials_get_components"
        "parentId" = "fld_materials"
        "modified" = $currentTime
        "created" = $currentTime
        "url" = "{{ _.base_url }}/api/v1/materials/components"
        "name" = "Get Unique Components"
        "description" = "Retrieve a list of all unique components found in materials."
        "method" = "GET"
        "body" = @{}
        "parameters" = @()
        "headers" = @()
        "authentication" = @{}
        "metaSortKey" = -1707868650000
        "isPrivate" = $false
        "settingStoreCookies" = $true
        "settingSendCookies" = $true
        "settingDisableRenderRequestBody" = $false
        "settingEncodeUrl" = $true
        "settingRebuildPath" = $true
        "settingFollowRedirects" = "global"
        "_type" = "request"
    }
}

# 2. Get All Prices (Costing)
if (-not (Test-RequestExists -Name "Get All Prices" -ParentId "fld_costing")) {
    $newRequests += @{
        "_id" = "req_costing_get_all_prices"
        "parentId" = "fld_costing"
        "modified" = $currentTime
        "created" = $currentTime
        "url" = "{{ _.base_url }}/api/v1/costing/prices"
        "name" = "Get All Prices"
        "description" = "Retrieve all component prices."
        "method" = "GET"
        "body" = @{}
        "parameters" = @()
        "headers" = @()
        "authentication" = @{}
        "metaSortKey" = -1707868750000
        "isPrivate" = $false
        "settingStoreCookies" = $true
        "settingSendCookies" = $true
        "settingDisableRenderRequestBody" = $false
        "settingEncodeUrl" = $true
        "settingRebuildPath" = $true
        "settingFollowRedirects" = "global"
        "_type" = "request"
    }
}

# 3. Get Prices Count (Costing)
if (-not (Test-RequestExists -Name "Get Prices Count" -ParentId "fld_costing")) {
    $newRequests += @{
        "_id" = "req_costing_get_prices_count"
        "parentId" = "fld_costing"
        "modified" = $currentTime
        "created" = $currentTime
        "url" = "{{ _.base_url }}/api/v1/costing/prices/count"
        "name" = "Get Prices Count"
        "description" = "Retrieve the total count of component prices."
        "method" = "GET"
        "body" = @{}
        "parameters" = @()
        "headers" = @()
        "authentication" = @{}
        "metaSortKey" = -1707868780000
        "isPrivate" = $false
        "settingStoreCookies" = $true
        "settingSendCookies" = $true
        "settingDisableRenderRequestBody" = $false
        "settingEncodeUrl" = $true
        "settingRebuildPath" = $true
        "settingFollowRedirects" = "global"
        "_type" = "request"
    }
}

# 4. Generate Prices (Populator)
if (-not (Test-RequestExists -Name "Generate Prices" -ParentId "fld_populator")) {
    $newRequests += @{
        "_id" = "req_populator_generate"
        "parentId" = "fld_populator"
        "modified" = $currentTime
        "created" = $currentTime
        "url" = "{{ _.base_url }}/api/v1/populator/prices/generate"
        "name" = "Generate Prices"
        "description" = "Trigger price generation for all components."
        "method" = "POST"
        "body" = @{}
        "parameters" = @()
        "headers" = @()
        "authentication" = @{}
        "metaSortKey" = -1707868800000
        "isPrivate" = $false
        "settingStoreCookies" = $true
        "settingSendCookies" = $true
        "settingDisableRenderRequestBody" = $false
        "settingEncodeUrl" = $true
        "settingRebuildPath" = $true
        "settingFollowRedirects" = "global"
        "_type" = "request"
    }
}

if ($newRequests.Count -gt 0) {
    $jsonContent.resources += $newRequests
    $jsonContent | ConvertTo-Json -Depth 100 | Set-Content -Path $filePath -Encoding utf8
    Write-Host "Added $($newRequests.Count) new requests."
} else {
    Write-Host "No new requests to add."
}

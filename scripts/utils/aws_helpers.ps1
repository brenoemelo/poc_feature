# AWS Helpers

function Get-AwsIdentity {
    param([string]$EndpointUrl)
    aws sts get-caller-identity --endpoint-url $EndpointUrl --no-cli-pager
}

function Assert-AwsConnection {
    param([string]$EndpointUrl = "http://localhost:4566")
    Write-Log "Verifying AWS Connection to $EndpointUrl..." -Level INFO
    aws sts get-caller-identity --endpoint-url $EndpointUrl --no-cli-pager 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Log "AWS Connection Verified." -Level SUCCESS
    } else {
        Write-Log "Failed to connect to AWS/LocalStack at $EndpointUrl." -Level ERROR
        throw "AWS Connection Failed"
    }
}

function Remove-AwsResource {
    param(
        [string]$Description,
        [scriptblock]$Action
    )
    
    Write-Log "Removing $Description..." -Level INFO
    # Temporarily relax ErrorActionPreference to capture CLI errors without throwing immediately
    $OldEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    
    try {
        $Output = & $Action 2>&1
        if ($LASTEXITCODE -ne 0) {
            $ErrorText = $Output | Out-String
            if ($ErrorText -match "ResourceNotFoundException" -or $ErrorText -match "NotFound" -or $ErrorText -match "does not exist") {
                Write-Log "Resource $Description not found (already deleted)." -Level INFO
            } else {
                Write-Log "Cleanup Warning for ${Description}: $ErrorText" -Level WARN
            }
        }
    } catch {
        Write-Log "Cleanup Exception for ${Description}: $_" -Level WARN
    } finally {
        $ErrorActionPreference = $OldEAP
    }
}

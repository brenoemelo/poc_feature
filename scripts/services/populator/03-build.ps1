# 03-build.ps1
param([string]$LogFile, [string]$ArtifactPath)
. "$PSScriptRoot/../../utils/common.ps1"
$Global:CurrentLogFile = $LogFile

Write-Log "STEP 3: Build" -Level INFO
. "$PSScriptRoot/config.local.ps1"

$PublishDir = "$PSScriptRoot/../../../publish/$($ServiceConfig.Name)"

# Dotnet Clean
Write-Log "Cleaning .NET Project..." -Level INFO
dotnet clean $ServiceConfig.ProjectPath -c Release
if ($LASTEXITCODE -ne 0) { throw "Dotnet Clean Failed" }

# Dotnet Publish
Write-Log "Publishing .NET Project..." -Level INFO
dotnet publish $ServiceConfig.ProjectPath -c Release -o $PublishDir -r linux-x64 --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "Dotnet Publish Failed" }

# Zip Artifact
$ZipPath = if ([string]::IsNullOrEmpty($ArtifactPath)) { "$PSScriptRoot/../../../$($ServiceConfig.Name).zip" } else { $ArtifactPath }
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

Compress-Archive -Path "$PublishDir/*" -DestinationPath $ZipPath -Force
if ($LASTEXITCODE -ne 0) { throw "Zip Creation Failed" }

Write-Log "Build Artifact Created: $ZipPath" -Level SUCCESS

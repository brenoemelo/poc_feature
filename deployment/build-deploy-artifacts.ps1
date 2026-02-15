Write-Host "Building deployment artifacts for all services..."

$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path

$services = @(
    @{ Name="PoC.Materials"; Project=(Join-Path $RepoRoot "src\PoC.Materials\PoC.Materials.csproj"); PublishDir=(Join-Path $RepoRoot "deploy\PoC.Materials"); Zip=(Join-Path $RepoRoot "deploy\PoC.Materials.zip") },
    @{ Name="PoC.Populator"; Project=(Join-Path $RepoRoot "src\PoC.Populator\PoC.Populator.csproj"); PublishDir=(Join-Path $RepoRoot "deploy\PoC.Populator"); Zip=(Join-Path $RepoRoot "deploy\PoC.Populator.zip") },
    @{ Name="PoC.Costing"; Project=(Join-Path $RepoRoot "src\PoC.Costing\PoC.Costing.csproj"); PublishDir=(Join-Path $RepoRoot "deploy\PoC.Costing"); Zip=(Join-Path $RepoRoot "deploy\PoC.Costing.zip") }
)

foreach ($svc in $services) {
    Write-Host "Publishing $($svc.Name)..."
    if (Test-Path $svc.PublishDir) { Remove-Item -Recurse -Force $svc.PublishDir }
    dotnet publish $svc.Project -c Release -o $svc.PublishDir -r linux-x64 --self-contained false

    Write-Host "Zipping $($svc.Name)..."
    if (Test-Path $svc.Zip) { Remove-Item -Force $svc.Zip }
    Compress-Archive -Path "$($svc.PublishDir)\*" -DestinationPath $svc.Zip
}

Write-Host "Artifacts ready under ./deploy. Ensure docker-compose mounts ./deploy to /opt/deploy."

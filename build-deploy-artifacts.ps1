Write-Host "Building deployment artifacts for all services..."

$services = @(
    @{ Name="PoC.Materials"; Project="src\PoC.Materials\PoC.Materials.csproj"; PublishDir="deploy\PoC.Materials"; Zip="deploy\PoC.Materials.zip" },
    @{ Name="PoC.Populator"; Project="src\PoC.Populator\PoC.Populator.csproj"; PublishDir="deploy\PoC.Populator"; Zip="deploy\PoC.Populator.zip" },
    @{ Name="PoC.Costing"; Project="src\PoC.Costing\PoC.Costing.csproj"; PublishDir="deploy\PoC.Costing"; Zip="deploy\PoC.Costing.zip" }
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

param(
    [switch]$SkipBuild
)

$RepoRoot = $PSScriptRoot
$LocalstackDeployScript = Join-Path $RepoRoot "localstack\deploy-all.ps1"

# 1. Build Phase
if (-not $SkipBuild) {
    Write-Host "--- STARTING PARALLEL BUILD ---" -ForegroundColor Cyan
    
    $services = @(
        @{ Name = "PoC.Materials"; Project = (Join-Path $RepoRoot "..\src\PoC.Materials\PoC.Materials.csproj"); PublishDir = (Join-Path $RepoRoot "..\deploy\PoC.Materials"); Zip = (Join-Path $RepoRoot "..\deploy\PoC.Materials.zip") },
        @{ Name = "PoC.Populator"; Project = (Join-Path $RepoRoot "..\src\PoC.Populator\PoC.Populator.csproj"); PublishDir = (Join-Path $RepoRoot "..\deploy\PoC.Populator"); Zip = (Join-Path $RepoRoot "..\deploy\PoC.Populator.zip") },
        @{ Name = "PoC.Costing"; Project = (Join-Path $RepoRoot "..\src\PoC.Costing\PoC.Costing.csproj"); PublishDir = (Join-Path $RepoRoot "..\deploy\PoC.Costing"); Zip = (Join-Path $RepoRoot "..\deploy\PoC.Costing.zip") }
    )

    # Pre-build Shared Projects First
    Write-Host "Pre-building shared libraries..." -ForegroundColor Yellow
    $sharedProjects = @(
        "..\src\PoC.Shared\PoC.Shared.csproj",
        "..\src\PoC.Shared.Infrastructure\PoC.Shared.Infrastructure.csproj"
    )

    foreach ($proj in $sharedProjects) {
        $projPath = Join-Path $RepoRoot $proj
        dotnet build $projPath -c Release -r linux-x64 --self-contained false
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $proj" }
    }

    # Build Services Sequentially
    Write-Host "Building services sequentially..." -ForegroundColor Yellow
    foreach ($svc in $services) {
        dotnet build $svc.Project -c Release -r linux-x64 --self-contained false
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $($svc.Name)" }
    }

    # Parallel Publish & Zip
    $jobs = @()
    foreach ($svc in $services) {
        $jobs += Start-Job -Name "Publish-$($svc.Name)" -ArgumentList $svc -ScriptBlock {
            param($s)
            
            if (Test-Path $s.PublishDir) { Remove-Item -Recurse -Force $s.PublishDir }
            dotnet publish $s.Project -c Release -o $s.PublishDir -r linux-x64 --self-contained false --no-build
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($s.Name)" }

            if (Test-Path $s.Zip) { Remove-Item -Force $s.Zip }
            Compress-Archive -Path "$($s.PublishDir)\*" -DestinationPath $s.Zip
            
            return "[$($s.Name)] Build & Zip Completed."
        }
    }

    Write-Host "Waiting for build jobs..."
    $results = $jobs | Wait-Job | Receive-Job
    $failed = $false
    foreach ($job in $jobs) {
        if ($job.State -ne 'Completed') { $failed = $true }
    }
    $results | ForEach-Object { Write-Host $_ }
    if ($failed) { throw "One or more build jobs failed." }

    Write-Host "--- BUILD COMPLETED ---" -ForegroundColor Green
}

# 2. Deploy Phase
Write-Host "--- STARTING DEPLOYMENT ---" -ForegroundColor Cyan
& $LocalstackDeployScript -SkipBuild
if ($LASTEXITCODE -ne 0) { throw "Deployment failed." }

Write-Host "--- FULL BUILD & DEPLOY COMPLETED ---" -ForegroundColor Green

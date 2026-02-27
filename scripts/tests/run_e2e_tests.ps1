$ErrorActionPreference = "Stop"

Write-Host "Building solution..."
dotnet build PoC.sln -c Debug

Write-Host "Running E2E tests..."
# 5 minutes timeout
$timeoutSeconds = 300 
$testProcess = Start-Process -FilePath "dotnet" -ArgumentList "test tests/PoC.E2E/PoC.E2E.csproj --logger console;verbosity=normal" -PassThru -NoNewWindow

$startTime = Get-Date
while (-not $testProcess.HasExited) {
    $currentTime = Get-Date
    if (($currentTime - $startTime).TotalSeconds -gt $timeoutSeconds) {
        Write-Host "Tests timed out! Killing process..."
        Stop-Process -Id $testProcess.Id -Force
        exit 1
    }
    Start-Sleep -Seconds 1
}

if ($testProcess.ExitCode -ne 0) {
    Write-Host "Tests failed with exit code $($testProcess.ExitCode)"
    exit $testProcess.ExitCode
}

Write-Host "All tests passed!"

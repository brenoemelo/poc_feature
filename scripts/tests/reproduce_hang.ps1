$ErrorActionPreference = "Stop"

Write-Host "Building project..."
dotnet build d:\Projetos\poc_feature\PoC.sln -c Debug

Write-Host "Running Materials_Lifecycle_HappyPath test..."
# Set a timeout for the test command itself to avoid hanging forever
$timeoutSeconds = 60
$testProcess = Start-Process -FilePath "dotnet" -ArgumentList "test d:\Projetos\poc_feature\tests\PoC.E2E\PoC.E2E.csproj --filter DisplayName~Materials_Lifecycle_HappyPath --logger console;verbosity=detailed" -PassThru -NoNewWindow

$startTime = Get-Date
while (-not $testProcess.HasExited) {
    $currentTime = Get-Date
    if (($currentTime - $startTime).TotalSeconds -gt $timeoutSeconds) {
        Write-Host "Test timed out! Killing process..."
        Stop-Process -Id $testProcess.Id -Force
        exit 1
    }
    Start-Sleep -Seconds 1
}

if ($testProcess.ExitCode -ne 0) {
    Write-Host "Test failed with exit code $($testProcess.ExitCode)"
    exit $testProcess.ExitCode
}

Write-Host "Test passed!"

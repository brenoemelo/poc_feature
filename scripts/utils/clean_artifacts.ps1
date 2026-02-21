
param(
    [string]$RootPath = "$PSScriptRoot/../../src"
)

Write-Host "Cleaning bin and obj folders in $RootPath..." -ForegroundColor Cyan

Get-ChildItem -Path $RootPath -Include bin,obj -Recurse -Directory | ForEach-Object {
    Write-Host "Deleting $($_.FullName)..." -ForegroundColor Yellow
    Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Cleanup completed." -ForegroundColor Green

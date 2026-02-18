
$ErrorActionPreference = "Stop"

$projectPath = "d:\Projetos\poc_feature\src\PoC.Populator"
$publishDir = "d:\Projetos\poc_feature\publish\populator"
$zipPath = "d:\Projetos\poc_feature\publish\populator.zip"
$functionName = "PoC-Populator-Worker"

Write-Host "Publishing PoC.Populator..."
dotnet publish $projectPath -c Release -o $publishDir --no-self-contained

Write-Host "Zipping..."
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath

Write-Host "Updating Lambda Function Code..."
aws --endpoint-url=http://localhost:4566 lambda update-function-code `
    --function-name $functionName `
    --zip-file "fileb://$zipPath"

Write-Host "Done."

# Builds DocumentLink for every supported Revit version and deploys each to its Addins folder.
# Usage:  ./build-all.ps1            (Release, deploy)
#         ./build-all.ps1 -NoDeploy  (Release, no copy — use while Revit is running)
param([switch]$NoDeploy)

$ErrorActionPreference = "Stop"
$deploy = if ($NoDeploy) { "false" } else { "true" }
foreach ($v in 2023, 2024, 2025, 2026) {
    Write-Host "=== Revit $v ===" -ForegroundColor Cyan
    dotnet build "$PSScriptRoot/src/DocumentLink/DocumentLink.csproj" -c Release -p:RevitVersion=$v -p:DeployToRevit=$deploy
    if ($LASTEXITCODE -ne 0) { throw "Build failed for Revit $v" }
}
Write-Host "All versions built." -ForegroundColor Green

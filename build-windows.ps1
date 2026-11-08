$ErrorActionPreference = 'Stop'
Write-Host 'ChaosXMB - Windows .NET 8 WPF build' -ForegroundColor Cyan
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet bulunamadı. .NET 8 SDK veya Visual Studio 2022 .NET Desktop Development kurun.'
}
dotnet restore "$PSScriptRoot/ChaosXMB.sln"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build "$PSScriptRoot/ChaosXMB.sln" -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host 'Build tamamlandı: ChaosXMB/bin/Release/net8.0-windows/' -ForegroundColor Green

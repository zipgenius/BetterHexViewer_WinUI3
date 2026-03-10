# pack.ps1 – Build and pack BetterHexViewer.WinUI3 NuGet package
# Usage:  .\build\pack.ps1 -Version 1.0.0
param(
    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0",

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [string]$Platform = "x64"
)

$ProjectFile = Join-Path $PSScriptRoot "..\src\BetterHexViewer.WinUI3\BetterHexViewer.WinUI3.csproj"
$OutputDir   = Join-Path $PSScriptRoot "..\nupkg"

Write-Host "=== BetterHexViewer.WinUI3 NuGet Pack ===" -ForegroundColor Cyan
Write-Host "  Version       : $Version"
Write-Host "  Configuration : $Configuration"
Write-Host "  Platform      : $Platform"
Write-Host ""

# Restore
Write-Host "[1/3] Restoring..." -ForegroundColor Yellow
dotnet restore $ProjectFile
if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed"; exit 1 }

# Build
Write-Host "[2/3] Building..." -ForegroundColor Yellow
dotnet build $ProjectFile -c $Configuration -p:Platform=$Platform --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

# Pack
Write-Host "[3/3] Packing..." -ForegroundColor Yellow
dotnet pack $ProjectFile `
    -c $Configuration `
    -p:Platform=$Platform `
    -p:Version=$Version `
    --no-restore `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) { Write-Error "Pack failed"; exit 1 }

Write-Host ""
Write-Host "✅ Package created in: $OutputDir" -ForegroundColor Green
Get-ChildItem $OutputDir -Filter "*.nupkg" | ForEach-Object {
    Write-Host "   $_" -ForegroundColor White
}

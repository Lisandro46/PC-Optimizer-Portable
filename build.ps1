$ErrorActionPreference = 'Stop'

# Genera el ejecutable portable self-contained (no requiere instalar .NET).
# Requiere el SDK de .NET 8 instalado (dotnet --list-sdks debe mostrar 8.x).

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'app\PCOptimizer.csproj'
$version = '2.0.0'
$releaseDir = Join-Path $root "release\PCOptimizerPortable-v$version"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "No se encontró 'dotnet'. Instalá el SDK de .NET 8: winget install Microsoft.DotNet.SDK.8"
}

Write-Host "Compilando portable v$version ..." -ForegroundColor Cyan

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o (Join-Path $root 'app\publish')

if ($LASTEXITCODE -ne 0) { throw "La compilación falló con código $LASTEXITCODE" }

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Copy-Item (Join-Path $root 'app\publish\PCOptimizer.exe') (Join-Path $releaseDir 'PCOptimizer.exe') -Force
if (Test-Path (Join-Path $root 'release\LEEME.txt')) {
    Copy-Item (Join-Path $root 'release\LEEME.txt') (Join-Path $releaseDir 'LEEME.txt') -Force
}

Write-Host "Portable creado en: $releaseDir\PCOptimizer.exe" -ForegroundColor Green

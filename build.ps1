$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$output = Join-Path $root 'dist'

if (-not (Test-Path $compiler)) {
    throw "No se encontró el compilador de .NET Framework: $compiler"
}

New-Item -ItemType Directory -Path $output -Force | Out-Null

$sources = Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$arguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:x64'
    '/optimize+'
    '/win32manifest:' + (Join-Path $root 'app.manifest')
    '/out:' + (Join-Path $output 'PCOptimizerPortable.exe')
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Web.Extensions.dll'
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "La compilación falló con código $LASTEXITCODE"
}

Copy-Item (Join-Path $root 'README.md') (Join-Path $output 'LEEME.txt') -Force
Write-Host "Portable creado en: $output\PCOptimizerPortable.exe"

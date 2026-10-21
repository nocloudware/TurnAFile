$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
$coreProject = Join-Path $root "TurnAFile.Core\TurnAFile.Core.csproj"
$dll = Join-Path $root "TurnAFile.Core\bin\Debug\net8.0-windows\TurnAFile.Core.dll"

if (-not (Test-Path $dll)) {
    & dotnet build $coreProject -v q --nologo | Out-Null
}
if (-not (Test-Path $dll)) { throw "no se encontró TurnAFile.Core.dll en $dll" }

Add-Type -Path $dll

$tmp = Join-Path $env:TEMP "TurnAFileOutputPathsSmoke"
if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

$source = Join-Path $tmp "video.mp4"
Set-Content -Path $source -Value "origen"

$target = [TurnAFile.Core.Helpers.ConversionHelpers]::GetTargetPath($source, "mp3")
if ($target -ne (Join-Path $tmp "video.mp3")) { throw "GetTargetPath devolvió '$target'" }
if ([System.IO.File]::Exists($target)) { throw "el destino no debería existir todavía" }

# Destino ya creado: en conversión por lotes se omite, en conversión directa se crea el duplicado
Set-Content -Path $target -Value "destino"
if (-not [System.IO.File]::Exists($target)) { throw "no se pudo crear el destino de prueba" }

$output = [TurnAFile.Core.Helpers.ConversionHelpers]::GetOutputPath($source, "mp3")
if ($output -ne (Join-Path $tmp "video (1).mp3")) { throw "GetOutputPath devolvió '$output' (se esperaba el duplicado numerado)" }

Write-Host "PASS: por lotes se omite el destino existente; en directa se crea el duplicado"
Remove-Item -Recurse -Force $tmp

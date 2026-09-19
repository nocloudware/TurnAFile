$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
$ffmpeg = Join-Path $root "TurnAFile.Windows\tools\FFmpeg\ffmpeg.exe"
if (-not (Test-Path $ffmpeg)) { throw "ffmpeg not found at $ffmpeg" }

$tmp = Join-Path $env:TEMP "TurnAFileNormalizeSmoke"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$quiet = Join-Path $tmp "quiet.wav"
$normalized = Join-Path $tmp "normalized.wav"

& $ffmpeg -hide_banner -y -f lavfi -i "sine=frequency=440:duration=30" -af "volume=-35dB" -ar 44100 -ac 2 $quiet 2>&1 | Out-Null
if (-not (Test-Path $quiet)) { throw "no se pudo generar el tono de prueba" }

function Get-InputI([string]$file) {
    $output = & $ffmpeg -hide_banner -i $file -af loudnorm=print_format=json -f null - 2>&1
    $json = ($output | Where-Object { $_ -match '"input_i"' }) -join ""
    if ($json -match '"input_i"\s*:\s*"(-?[0-9.]+)"') { return [double]$matches[1] }
    throw "no se pudo medir la sonoridad de $file"
}

$before = Get-InputI $quiet

# Mismo comando que produce la app: wav -> wav, mismo codec, con loudnorm a Broadcast (-16)
& $ffmpeg -hide_banner -y -i $quiet -vn -c:a pcm_s16le -af "loudnorm=I=-16:TP=-1.5:LRA=11" $normalized 2>&1 | Out-Null
if (-not (Test-Path $normalized)) { throw "la normalización no produjo salida" }

$after = Get-InputI $normalized

if ($before -gt -30.0) { throw "tono de prueba no es suficientemente bajo (media $before LUFS)" }
if ($after -lt -17.0 -or $after -gt -15.0) { throw "salida fuera de rango: $after LUFS (se esperaba ~-16)" }

Write-Host "PASS: $before LUFS -> $after LUFS"
Remove-Item -Recurse -Force $tmp

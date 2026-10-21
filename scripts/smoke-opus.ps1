# Verificacion de conversion de audio hacia y desde Opus.
# Uso: pwsh -NoProfile -File scripts/smoke-opus.ps1
$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$coreDll = Join-Path $repo "TurnAFile.Core\bin\Debug\net8.0-windows\TurnAFile.Core.dll"
if (-not (Test-Path $coreDll)) { throw "No se encontro $coreDll (compila primero)" }
Add-Type -Path $coreDll

$builder = New-Object TurnAFile.Core.Services.FfmpegCommandBuilder
$videoQuality = [TurnAFile.Core.Services.VideoQuality]::Medium
$imageScaling = [TurnAFile.Core.Services.ImageScaling]::Original
$loudness = [TurnAFile.Core.Services.LoudnessTarget]::Broadcast

function Build-AudioCommand([string]$inPath, [string]$outPath, [string]$format, [string]$quality) {
    $audioQuality = switch ($quality) {
        "High" { [TurnAFile.Core.Services.AudioQuality]::High }
        "Medium" { [TurnAFile.Core.Services.AudioQuality]::Medium }
        "Low" { [TurnAFile.Core.Services.AudioQuality]::Low }
    }
    return $builder.BuildCommand($inPath, $outPath, $format, $videoQuality, $audioQuality, $imageScaling, $false, $loudness)
}

function Invoke-FFmpeg([string]$ffmpeg, [string]$argString) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $ffmpeg
    $psi.Arguments = $argString
    $psi.UseShellExecute = $false
    $psi.RedirectStandardError = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $err = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    $tail = ($err -split "`r?`n" | Where-Object { $_.Trim() -ne "" } | Select-Object -Last 4) -join " | "
    return @{ Code = $p.ExitCode; Error = $tail }
}

# 1) La calidad se traduce en bitrate fijo de Opus
foreach ($case in @(@{ Q = "High"; Bitrate = "192k" }, @{ Q = "Medium"; Bitrate = "128k" }, @{ Q = "Low"; Bitrate = "96k" })) {
    $cmd = Build-AudioCommand "in.mp3" "out.opus" "opus" $case.Q
    if ($cmd -notmatch "-c:a libopus") { throw "Falta '-c:a libopus' en: $cmd" }
    if ($cmd -notmatch "-b:a $($case.Bitrate)") { throw "Se esperaba -b:a $($case.Bitrate) en: $cmd" }
    if ($cmd -notmatch "-vn") { throw "Falta '-vn' en: $cmd" }
}

# 2) La extension .opus es una extension conocida
if ([TurnAFile.Core.Helpers.ConversionHelpers]::NormalizeExtension("opus") -ne "opus") {
    throw "NormalizeExtension('opus') no devolvio 'opus'"
}
$target = [TurnAFile.Core.Helpers.ConversionHelpers]::GetTargetPath("C:\tmp\cancion.mp3", "opus")
if ($target -ne "C:\tmp\cancion.opus") { throw "GetTargetPath devolvio '$target'" }

# 3) Conversion real: wav -> opus -> mp3
$ffmpeg = Join-Path $repo "TurnAFile.Windows\tools\FFmpeg\ffmpeg.exe"
if (-not (Test-Path $ffmpeg)) { throw "No se encontro ffmpeg.exe en $ffmpeg" }

$tmp = Join-Path $env:TEMP ("turnafile-opus-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $wav = Join-Path $tmp "tono.wav"
    $opus = Join-Path $tmp "tono.opus"
    $mp3 = Join-Path $tmp "tono.mp3"

    $gen = Invoke-FFmpeg $ffmpeg "-y -f lavfi -i sine=frequency=440:duration=1 -c:a pcm_s16le `"$wav`""
    if ($gen.Code -ne 0 -or -not (Test-Path $wav)) { throw "No se genero el wav de prueba: $($gen.Error)" }

    $toOpus = Invoke-FFmpeg $ffmpeg (Build-AudioCommand $wav $opus "opus" "Medium")
    if ($toOpus.Code -ne 0 -or -not (Test-Path $opus)) { throw "Fallo la conversion a opus: $($toOpus.Error)" }
    if ((Get-Item $opus).Length -le 0) { throw "El archivo opus quedo vacio" }

    $header = [System.Text.Encoding]::ASCII.GetString((Get-Content -LiteralPath $opus -AsByteStream -TotalCount 4))
    if ($header -ne "OggS") { throw "El archivo opus no es un contenedor Ogg valido (cabecera: '$header')" }

    $toMp3 = Invoke-FFmpeg $ffmpeg (Build-AudioCommand $opus $mp3 "mp3" "Medium")
    if ($toMp3.Code -ne 0 -or -not (Test-Path $mp3)) { throw "Fallo la conversion desde opus: $($toMp3.Error)" }
    if ((Get-Item $mp3).Length -le 0) { throw "El archivo mp3 quedo vacio" }

    # 4) Extraccion de audio desde video hacia opus
    $video = Join-Path $tmp "clip.mp4"
    $videoOpus = Join-Path $tmp "clip.opus"
    $genVideo = Invoke-FFmpeg $ffmpeg "-y -f lavfi -i testsrc=duration=1:size=160x120:rate=10 -f lavfi -i sine=frequency=440:duration=1 -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest `"$video`""
    if ($genVideo.Code -ne 0 -or -not (Test-Path $video)) { throw "No se genero el video de prueba: $($genVideo.Error)" }

    $fromVideo = Invoke-FFmpeg $ffmpeg (Build-AudioCommand $video $videoOpus "opus" "Medium")
    if ($fromVideo.Code -ne 0 -or -not (Test-Path $videoOpus)) { throw "Fallo la extraccion de audio a opus: $($fromVideo.Error)" }
    if ((Get-Item $videoOpus).Length -le 0) { throw "El opus extraido del video quedo vacio" }

    Write-Output "PASS: opus (hacia y desde, y desde video) con bitrate 192k/128k/96k"
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

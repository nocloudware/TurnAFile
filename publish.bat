@echo off
setlocal enabledelayedexpansion

echo ============================================
echo  TurnAFile - Build Script
echo ============================================
echo.

set PROJECT_DIR=%~dp0
set OUTPUT_DIR=%PROJECT_DIR%publish\TurnAFile
set CONFIG=Release
set FRAMEWORK=net8.0-windows

:: Clean previous build
if exist "%OUTPUT_DIR%" (
    echo [1/5] Cleaning previous build...
    rd /s /q "%OUTPUT_DIR%"
)

:: Publish
echo [2/5] Publishing Release...
dotnet publish "%PROJECT_DIR%TurnAFile.Windows\TurnAFile.Windows.csproj" ^
    -c %CONFIG% ^
    -f %FRAMEWORK% ^
    -r win-x64 ^
    --no-self-contained ^
    -p:PublishReadyToRun=false ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o "%OUTPUT_DIR%"

if errorlevel 1 (
    echo.
    echo ERROR: Build failed.
    pause
    exit /b 1
)

:: Clean debug files
echo [3/5] Cleaning debug files...
cd /d "%OUTPUT_DIR%"
del /q /s *.pdb >nul 2>&1
del /q /s *.xml >nul 2>&1

:: Download FFmpeg if not present (external binary, not in repo)
if not exist "%OUTPUT_DIR%\tools\FFmpeg\ffmpeg.exe" (
    echo [4/5] Downloading FFmpeg...
    if not exist "%OUTPUT_DIR%\tools\FFmpeg" mkdir "%OUTPUT_DIR%\tools\FFmpeg"
    powershell -Command "$ErrorActionPreference='Continue'; $url='https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip'; $zip=$env:TEMP+'\ffmpeg.zip'; Write-Host '  Downloading...'; try { Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing } catch { }; if (-not (Test-Path $zip)) { curl -L -o $zip $url }; Write-Host '  Extracting...'; Add-Type -AssemblyName System.IO.Compression.FileSystem; $z=[System.IO.Compression.ZipFile]::OpenRead($zip); $targets=@('ffmpeg.exe','LICENSE.txt','README.md','CREDITS'); foreach ($t in $targets) { $e=$z.Entries|Where{$_.Name -eq $t}|Select-Object -First 1; if ($e) { $dest='%OUTPUT_DIR%\tools\FFmpeg\'+$t; [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e,$dest,$true); Write-Host ('  '+$t) } }; $z.Dispose(); Remove-Item $zip -EA 0; Write-Host 'FFmpeg ready.'"
)

:: Copy Tesseract OCR language data
if not exist "%OUTPUT_DIR%\tools\tessdata" (
    echo Copying Tesseract language data...
    if not exist "%OUTPUT_DIR%\tools\tessdata" mkdir "%OUTPUT_DIR%\tools\tessdata"
    xcopy /E /I /Y "%PROJECT_DIR%TurnAFile.Windows\tools\tessdata\*.traineddata" "%OUTPUT_DIR%\tools\tessdata\" >nul
)

:: Copy third-party licenses
echo Copying licenses...
if not exist "%OUTPUT_DIR%\Licenses" mkdir "%OUTPUT_DIR%\Licenses"
xcopy /Y "%PROJECT_DIR%TurnAFile.Windows\Libs\*.txt" "%OUTPUT_DIR%\Licenses\" >nul 2>nul
xcopy /Y "%PROJECT_DIR%TurnAFile.Windows\THIRD_PARTY_NOTICES.txt" "%OUTPUT_DIR%\Licenses\" >nul 2>nul

:: Copy documentation
if not exist "%OUTPUT_DIR%\docs" mkdir "%OUTPUT_DIR%\docs"
xcopy /Y "%PROJECT_DIR%TurnAFile.Windows\docs\*.txt" "%OUTPUT_DIR%\docs\" >nul 2>nul
xcopy /Y "%PROJECT_DIR%LICENSE.txt" "%OUTPUT_DIR%\docs\" >nul 2>&1

:: Calculate size
echo [5/5] Calculating size...
for /f "tokens=3" %%a in ('powershell -Command "(Get-ChildItem '%OUTPUT_DIR%' -Recurse -File | Measure-Object Length -Sum).Sum / 1MB" 2^>nul') do set SIZE_MB=%%a

echo.
echo ============================================
echo  Build Complete!
echo ============================================
echo.
echo  Output:  %OUTPUT_DIR%
echo  Size:    %SIZE_MB% MB
echo.
echo  Contents:
echo    - TurnAFile.exe (main app)
echo    - tools\FFmpeg\ (video/audio/image engine)
echo    - tools\tessdata\ (OCR - 6 languages)
echo    - Licenses\ (third-party licenses)
echo    - docs\ (documentation)
echo.
echo  Create installer:
echo    1. Install Inno Setup 6
echo    2. Open installer\TurnAFile.iss
echo    3. Compile
echo.
pause
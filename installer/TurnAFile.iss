; ============================================================
; TurnAFile - Instalador (Inno Setup 6)
; ============================================================
; TurnAFile es software gratuito.
; https://github.com/nocloudware/TurnAFile
; ============================================================
; IMPORTANTE: compilar desde la raiz del repo:
;   iscc installer\TurnAFile.iss
; Todas las rutas son relativas a la raiz del repo.
; ============================================================

#define MyAppName "TurnAFile"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "TurnAFile"
#define MyAppURL "https://github.com/nocloudware/TurnAFile"
#define MyAppExeName "TurnAFile.exe"

#define PublishRoot ".."
#define PublishDir "..\publish\TurnAFile"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=TurnAFile-{#MyAppVersion}-Setup
SetupIconFile=..\TurnAFile.Windows\Assets\icon-toggles.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=no
WizardStyle=modern
WizardSizePercent=100,100
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main application
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; All DLLs, JSON, and satellite resource folders (es, fr, de, ja, pt, zh)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "tools\*,docs\*,*.pdb,*.xml"

; FFmpeg
Source: "{#PublishDir}\tools\FFmpeg\*"; DestDir: "{app}\tools\FFmpeg"; Flags: ignoreversion recursesubdirs

; Tesseract OCR language data
Source: "{#PublishDir}\tools\tessdata\*"; DestDir: "{app}\tools\tessdata"; Flags: ignoreversion

; Documentation
Source: "{#PublishDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove our files only - do NOT touch user data in other locations
Type: files; Name: "{app}\{#MyAppExeName}"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\tools"
Type: filesandordirs; Name: "{app}\Licenses"
Type: filesandordirs; Name: "{app}\docs"
; Clean user settings on uninstall
Type: filesandordirs; Name: "{localappdata}\TurnAFile"



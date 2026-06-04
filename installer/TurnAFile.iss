; ============================================================
; TurnAFile - Instalador (Inno Setup 6)
; ============================================================
; TurnAFile es software gratuito.
; https://github.com/nocloudware/TurnAFile
; ============================================================

#define MyAppName "TurnAFile"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "TurnAFile"
#define MyAppURL "https://github.com/nocloudware/TurnAFile"
#define MyAppExeName "TurnAFile.exe"

; Ruta de salida del publish (ajustar si es necesario)
#define PublishDir "C:\Users\chand\Desktop\Proyectos\TurnAFile\publish\TurnAFile"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

OutputDir=installer\output
OutputBaseFilename=TurnAFile-{#MyAppVersion}-Setup
SetupIconFile=..\TurnAFile.Windows\Assets\icon-toggles.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=100,100

; Requiere Windows 10 o superior (para .NET 8)
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
Name: "contextmenu"; Description: "Register right-click context menu in Explorer"; GroupDescription: "Integration:"; Flags: checkedonce

[Files]
; Main application
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\*.json"; DestDir: "{app}"; Flags: ignoreversion

; FFmpeg (video/audio/image conversion engine)
Source: "{#PublishDir}\tools\FFmpeg\*"; DestDir: "{app}\tools\FFmpeg"; Flags: ignoreversion recursesubdirs

; Tesseract OCR language data (6 languages: en,es,fr,de,ja,zh)
Source: "{#PublishDir}\tools\tessdata\*"; DestDir: "{app}\tools\tessdata"; Flags: ignoreversion

; Licenses and attributions
Source: "{#PublishDir}\THIRD_PARTY_NOTICES.txt"; DestDir: "{app}\"; Flags: ignoreversion

; Documentation
Source: "{#PublishDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#MyAppExeName}"; Parameters: "--reinstall"; Flags: runhidden; Tasks: contextmenu

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

[Code]
// Check if .NET 8 Desktop Runtime is installed
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if not FileExists(ExpandConstant('{system}\hostfxr.dll')) then
  begin
    if MsgBox('TurnAFile requires .NET 8 Desktop Runtime.'#13#10
              'The download page will open.'#13#10#13#10
              'Install it and run this installer again.'#13#10#13#10
              'Open download page now?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0',
        '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
    Result := False;
  end;
end;

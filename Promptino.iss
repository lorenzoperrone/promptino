; Promptino Inno Setup Script
; Richiede Inno Setup 7+ (https://jrsoftware.org/isdl.php)
;
; Uso:
;   1. Eseguire build-release.ps1 per generare i binari in release/binaries/
;   2. Aprire questo script con Inno Setup Compiler e compilare
;   Oppure da riga: iscc Promptino.iss

#define MyAppName "Promptino"
; La versione può essere sovrascritta con la variabile d'ambiente PROMPTINO_VERSION
#ifndef MyAppVersion
# define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "Promptino"
#define MyAppURL "https://github.com/lorenzoperrone/promptino"
#define MyAppExeName "Promptino.App.exe"

[Setup]
AppId={{B8F4C3E1-7A2D-4E9F-9C5D-3E1F2A8B0C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=release\installer
OutputBaseFilename=Promptino-{#MyAppVersion}-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Messages]
english.SetupAppTitle=Promptino {#MyAppVersion} Setup
english.SetupWindowTitle=Promptino {#MyAppVersion} Setup
[CustomMessages]
english.LaunchPromptino=Launch Promptino
italian.LaunchPromptino=Avvia Promptino

[Files]
; Binario principale (single-file self-contained)
Source: "release\binaries\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Native DLL obbligatori (rendering)
Source: "release\binaries\libSkiaSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\binaries\libHarfBuzzSharp.dll"; DestDir: "{app}"; Flags: ignoreversion

; NOTA: av_libglesv2.dll è un fallback OpenGL ES non necessario su Windows 10+ con DirectX.
; Viene deliberatamente escluso per risparmiare ~5 MB.
; I file .pdb (simboli di debug) sono esclusi perché non servono in produzione.

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "desktopicon"; Description: "Crea un collegamento sul desktop"; GroupDescription: "Collegamenti aggiuntivi:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchPromptino}"; Flags: postinstall nowait skipifsilent unchecked

[Code]

const
  VC_REDIST_URL = 'https://aka.ms/vcredist';

{ Controlla se Visual C++ Redistributable è installato }
function IsVCRedistInstalled: Boolean;
var
  Installed: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\VC\Runtimes\vcredist\CurrentVersion', 'Installed', Installed) then
    Result := (Installed = 1);
  if not Result then
    if RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\VisualStudio\VC\Runtimes\vcredist\CurrentVersion', 'Installed', Installed) then
      Result := (Installed = 1);
end;

function InitializeSetup: Boolean;
var
  ResultCode: Integer;
  ErrorMsg: string;
begin
  if not IsVCRedistInstalled then
  begin
    if MsgBox(
      'Promptino requires the Microsoft Visual C++ Redistributable.'#13#13 +
      'Without it, the application may fail to start.'#13#13 +
      'Do you want to download it now?',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', VC_REDIST_URL, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
      MsgBox(
        'Please install the Visual C++ Redistributable, then run this installer again.',
        mbInformation, MB_OK);
      Result := False;
    end
    else
    begin
      MsgBox(
        'The installer will continue, but Promptino may not start correctly '#13 +
        'without the Visual C++ Redistributable.'#13#13 +
        'You can install it later from: https://aka.ms/vcredist',
        mbInformation, MB_OK);
      Result := True;
    end;
  end
  else
    Result := True;
end;

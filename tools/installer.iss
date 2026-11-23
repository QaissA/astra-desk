; Builds dist\AstraDesk-Setup.exe from dist\DesktopStyler.exe (run by tools\publish.ps1).
; Per-user install, no admin: Start menu entry (searchable), optional desktop shortcut, uninstall from Settings > Apps.
#define AppExe "..\dist\DesktopStyler.exe"
#define AppVersion GetStringFileInfo(AppExe, "ProductVersion")

[Setup]
AppId={{6F1C2A7E-5B3D-4E8A-9C1F-DE57A1E5C0B1}
AppName=Astra Desk
AppVersion={#AppVersion}
AppVerName=Astra Desk {#AppVersion}
AppPublisher=Astra Desk
; must match Installer.InstallDir in Program.cs, or the app thinks it isn't installed and shows its own install prompt
DefaultDirName={localappdata}\Programs\DesktopStyler
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=AstraDesk-Setup
SetupIconFile=..\assets\app.ico
; logo in the wizard's corner (built by tools\make-icon.ps1, straight alpha)
WizardSmallImageFile=..\assets\wizard-small.bmp
WizardImageAlphaFormat=defined
UninstallDisplayIcon={app}\DesktopStyler.exe
UninstallDisplayName=Astra Desk
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Astra Desk"; Filename: "{app}\DesktopStyler.exe"
Name: "{userdesktop}\Astra Desk"; Filename: "{app}\DesktopStyler.exe"; Tasks: desktopicon

[InstallDelete]
; shortcuts from before the rename to Astra Desk
Type: files; Name: "{userprograms}\Desktop Styler.lnk"
Type: files; Name: "{userdesktop}\Desktop Styler.lnk"

[Registry]
; drop the Apps entry left by the app's old built-in installer so it isn't listed twice
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopStyler"; Flags: deletekey dontcreatekey

[Run]
Filename: "{app}\DesktopStyler.exe"; Description: "Launch Astra Desk"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; restores the taskbar, desktop icons, startup entry and screensaver; keeps settings and downloaded wallpapers
Filename: "{app}\DesktopStyler.exe"; Parameters: "--uninstall --quiet"; Flags: waituntilterminated runhidden; RunOnceId: "RestoreWindows"

[Code]
// a running copy hides the taskbar; ask it to quit cleanly (it restores the taskbar) before files are replaced
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  if FileExists(ExpandConstant('{app}\DesktopStyler.exe')) then
  begin
    Exec(ExpandConstant('{app}\DesktopStyler.exe'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
    Sleep(2000);
  end;
  Result := '';
end;

; Script generated for Inno Setup 6
; Steam Route Fixer & Traffic Inspector Setup
; Created by TXA Studio

#define MyAppName "Steam Route Fixer"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "TXA Studio"
#define MyAppURL "https://github.com/TXAVL/SteamRouteFixer"
#define MyAppExeName "SteamRouteFixer.exe"
#define MyAppId "{{8B4A2D0E-7C21-4F2A-8991-3E512BC61099}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE.txt
OutputDir=setup_output
OutputBaseFilename=SteamRouteFixer_Setup_v{#MyAppVersion}
SetupIconFile=Assets\app.ico
Compression=lzma
SolidCompression=no
WizardStyle=modern
ShowLanguageDialog=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=auto
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "vietnamese"; MessagesFile: "Languages\Vietnamese.isl"; LicenseFile: "LICENSE.txt"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\app.ico"

[Registry]
; Register .txal and legacy .txa file associations
Root: HKA; Subkey: "Software\Classes\.txal"; ValueType: string; ValueName: ""; ValueData: "TxaLanguagePackageFile"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\.txa"; ValueType: string; ValueName: ""; ValueData: "TxaLanguagePackageFile"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\TxaLanguagePackageFile"; ValueType: string; ValueName: ""; ValueData: "TXA Language Package File"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\TxaLanguagePackageFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\TxaLanguagePackageFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  SHCNE_ASSOCCHANGED = $08000000;
  SHCNF_IDLIST = 0;

procedure SHChangeNotify(wEventId: LongInt; uFlags: Cardinal; dwItem1: Cardinal; dwItem2: Cardinal);
external 'SHChangeNotify@shell32.dll stdcall';

// Function to check if a process is running using tasklist
function IsProcessRunning(const FileName: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/c tasklist /FI "IMAGENAME eq ' + FileName + '" | find /I "' + FileName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

// Function to stop running process using taskkill
procedure TerminateAppProcess(const FileName: string);
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM ' + FileName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

// Function to uninstall previous version cleanly
function GetPreviousUninstallString(): String;
var
  UninstallPath: String;
begin
  UninstallPath := '';
  if not RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + '{#MyAppId}' + '_is1', 'UninstallString', UninstallPath) then
  begin
    RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + '{#MyAppId}' + '_is1', 'UninstallString', UninstallPath);
  end;
  Result := UninstallPath;
end;

function InitializeSetup(): Boolean;
var
  OldUninstallString: String;
  ResultCode: Integer;
begin
  Result := True;

  // 1. Check and terminate active running application
  if IsProcessRunning('{#MyAppExeName}') then
  begin
    if MsgBox('Ứng dụng ' + '{#MyAppName}' + ' đang chạy. Trình cài đặt sẽ tự động đóng ứng dụng trước khi tiếp tục. Bạn có muốn tiếp tục?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      TerminateAppProcess('{#MyAppExeName}');
    end
    else
    begin
      Result := False;
      Exit;
    end;
  end;

  // 2. Check and cleanly uninstall previous version if detected
  OldUninstallString := GetPreviousUninstallString();
  if OldUninstallString <> '' then
  begin
    OldUninstallString := RemoveQuotes(OldUninstallString);
    if FileExists(OldUninstallString) then
    begin
      Exec(OldUninstallString, '/SILENT /VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(1000);
    end;
  end;
end;

// Clean up registry and file association completely upon uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Clean up .txal and .txa from HKCU and HKLM explicitly
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.txal');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.txa');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\TxaLanguagePackageFile');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Applications\' + '{#MyAppExeName}');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txal');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txa');

    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\.txal');
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\.txa');
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\TxaLanguagePackageFile');
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\Applications\' + '{#MyAppExeName}');

    // Notify Windows Explorer immediately that file associations have changed
    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0);
  end;
end;

#define AppName "Snug for Windows"
#define AppVersion "0.2.27"
#define AppExeName "SnugLauncher.exe"

[Setup]
AppId={{6A9A3C8D-9C6F-4F8B-AF5E-21B6A5B5F4E1}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\Snug
DefaultGroupName=Snug
UninstallDisplayIcon={app}\Snug.ico
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.18362
PrivilegesRequired=admin
OutputDir=..\artifacts\installer
OutputBaseFilename=Snug-Setup-{#AppVersion}-x64
SetupIconFile=assets\Snug.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式"; Flags: unchecked

[Files]
Source: "..\artifacts\package\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Snug\Snug"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Snug.ico"
Name: "{autodesktop}\Snug"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Snug.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 Snug"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Driver\Install-SnugDriver.ps1"" -Action Uninstall"; WorkingDir: "{app}"; Flags: runhidden waituntilterminated; RunOnceId: "SnugDriverUninstall"; Check: HasSnugDriverMarker

[Code]
function IsSnugRunning: Boolean;
begin
  Result := CheckForMutexes('Local\SnugForWindows');
end;

function HasSnugDriverMarker: Boolean;
begin
  Result := FileExists(ExpandConstant('{commonappdata}\Snug\driver-ownership.json'));
end;

function InitializeSetup: Boolean;
begin
  Result := not IsSnugRunning;
  if not Result then
    MsgBox('请先退出 Snug，再安装或更新。', mbError, MB_OK);
end;

function InitializeUninstall: Boolean;
begin
  Result := not IsSnugRunning;
  if not Result then
    MsgBox('请先退出 Snug，再卸载。', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  PowerShellPath: String;
  Parameters: String;
begin
  if CurStep = ssPostInstall then
  begin
    PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
    Parameters := '-NoProfile -ExecutionPolicy Bypass -File "' +
      ExpandConstant('{app}\Driver\Install-SnugDriver.ps1') + '" -Action Install';

    if not Exec(PowerShellPath, Parameters, ExpandConstant('{app}'),
      SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('无法启动 Snug 虚拟显示驱动安装程序。');

    if ResultCode <> 0 then
      RaiseException('Snug 虚拟显示驱动安装失败。请查看 %ProgramData%\Snug\Logs\driver-install.log。');
  end;
end;
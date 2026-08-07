; ScanBridge Installer Script
; Inno Setup 6+

#define MyAppName "ScanBridge"
#define MyAppVersion "2.0.6"
#define MyAppPublisher "ScanBridge"
#define MyAppURL "https://github.com/makendorf/scan-bridge"
#define MyAppServiceName "ScanBridge"
#define MyAppExeName "ScanBridge.exe"
#define MyAppTrayExeName "ScanBridgeTray.exe"
#define RuntimeVersion "10.0.9"
#define DotNetRuntimeInstaller "dotnet-runtime-10.0.9-win-x64.exe"
#define AspNetCoreRuntimeInstaller "aspnetcore-runtime-10.0.9-win-x64.exe"
#define WindowsDesktopRuntimeInstaller "windowsdesktop-runtime-10.0.9-win-x64.exe"

[Setup]
AppId={{B8F3E4A2-1C5D-4E7F-9A2B-3C6D8E0F1A4B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=ScanBridge_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "installservice"; Description: "Установить как Windows Service"; GroupDescription: "Дополнительно:"; Flags: checkedonce

[Files]
Source: "D:\Project\ScanBridge\src\bin\Release\net10.0\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#DotNetRuntimeInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#AspNetCoreRuntimeInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#WindowsDesktopRuntimeInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Registry]
; Автозапуск tray-приложения при входе пользователя
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ScanBridgeTray"; ValueData: """{app}\{#MyAppTrayExeName}"""; Flags: uninsdeletevalue

[Icons]
Name: "{group}\{#MyAppName} (Web UI)"; Filename: "http://localhost:2305"
Name: "{group}\{#MyAppName} Tray"; Filename: "{app}\{#MyAppTrayExeName}"
Name: "{group}\Папка приложения"; Filename: "{app}"
Name: "{group}\Удалить {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
; .NET Runtime
Filename: "{tmp}\{#DotNetRuntimeInstaller}"; Parameters: "/install /quiet /norestart"; StatusMsg: "Установка .NET Runtime {#RuntimeVersion}..."; Flags: waituntilterminated; Check: not IsDotNetRuntimeInstalled
; ASP.NET Core Runtime
Filename: "{tmp}\{#AspNetCoreRuntimeInstaller}"; Parameters: "/install /quiet /norestart"; StatusMsg: "Установка ASP.NET Core Runtime {#RuntimeVersion}..."; Flags: waituntilterminated; Check: not IsAspNetCoreRuntimeInstalled
; Windows Desktop Runtime
Filename: "{tmp}\{#WindowsDesktopRuntimeInstaller}"; Parameters: "/install /quiet /norestart"; StatusMsg: "Установка Windows Desktop Runtime {#RuntimeVersion}..."; Flags: waituntilterminated; Check: not IsWindowsDesktopRuntimeInstalled
; Windows Service
Filename: "sc"; Parameters: "create {#MyAppServiceName} binPath= ""{app}\{#MyAppExeName}"""; Tasks: installservice; Flags: runhidden
Filename: "sc"; Parameters: "description {#MyAppServiceName} ""Система управления сканерами штрихкодов и QR-кодов"""; Tasks: installservice; Flags: runhidden
Filename: "sc"; Parameters: "start {#MyAppServiceName}"; Tasks: installservice; Flags: runhidden
; Tray-приложение
Filename: "{app}\{#MyAppTrayExeName}"; Flags: nowait postinstall skipifsilent; Description: "Запустить ScanBridge Tray"

[UninstallRun]
; Остановить tray-приложение перед удалением службы
Filename: "taskkill"; Parameters: "/F /IM {#MyAppTrayExeName}"; Flags: runhidden
Filename: "sc"; Parameters: "stop {#MyAppServiceName}"; Flags: runhidden
Filename: "sc"; Parameters: "delete {#MyAppServiceName}"; Flags: runhidden

[Code]
function IsDotNetRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
  Output: AnsiString;
  TempFile: String;
  ExecResult: Boolean;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\dotnet_check.txt');

  ExecResult := Exec(
    'cmd.exe',
    '/C dotnet --list-runtimes | findstr "Microsoft.NETCore.App" > "' + TempFile + '" 2>&1',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode
  );

  if ExecResult and (ResultCode = 0) then
  begin
    Result := LoadStringFromFile(TempFile, Output);
    if Result then
      Result := Pos('{#RuntimeVersion}', Output) > 0;
  end;

  DeleteFile(TempFile);
end;

function IsAspNetCoreRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
  Output: AnsiString;
  TempFile: String;
  ExecResult: Boolean;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\aspnetcore_check.txt');

  ExecResult := Exec(
    'cmd.exe',
    '/C dotnet --list-runtimes | findstr "AspNetCore" > "' + TempFile + '" 2>&1',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode
  );

  if ExecResult and (ResultCode = 0) then
  begin
    Result := LoadStringFromFile(TempFile, Output);
    if Result then
      Result := Pos('{#RuntimeVersion}', Output) > 0;
  end;

  DeleteFile(TempFile);
end;

function IsWindowsDesktopRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
  Output: AnsiString;
  TempFile: String;
  ExecResult: Boolean;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\desktop_check.txt');

  ExecResult := Exec(
    'cmd.exe',
    '/C dotnet --list-runtimes | findstr "WindowsDesktop" > "' + TempFile + '" 2>&1',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode
  );

  if ExecResult and (ResultCode = 0) then
  begin
    Result := LoadStringFromFile(TempFile, Output);
    if Result then
      Result := Pos('{#RuntimeVersion}', Output) > 0;
  end;

  DeleteFile(TempFile);
end;

function InitializeSetup: Boolean;
var
  ResultCode: Integer;
  ServiceExists: Boolean;
begin
  Result := True;

  ServiceExists := Exec('sc', 'query ' + '{#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);

  if ServiceExists then
  begin
    Exec('sc', 'stop ' + '{#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('sc', 'delete ' + '{#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
  end;
end;

function InitializeUninstall: Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  Exec('taskkill', '/F /IM {#MyAppTrayExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc', 'stop ' + '{#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc', 'delete ' + '{#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

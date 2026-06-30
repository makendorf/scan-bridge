; ScanBridge Installer Script
; Inno Setup 6+

#define MyAppName "ScanBridge"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ScanBridge"
#define MyAppURL "https://github.com/ScanBridge/ScanBridge"
#define MyAppServiceName "ScanBridge"
#define MyAppExeName "ScanBridge.exe"
#define AspNetCoreVersion "10.0.9"
#define AspNetCoreInstaller "aspnetcore-runtime-10.0.9-win-x64.exe"

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
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AspNetCoreInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName} (Web UI)"; Filename: "http://localhost:5000"
Name: "{group}\Папка приложения"; Filename: "{app}"
Name: "{group}\Удалить {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{tmp}\{#AspNetCoreInstaller}"; Parameters: "/install /quiet /norestart"; StatusMsg: "Установка ASP.NET Core Runtime..."; Flags: waituntilterminated; Check: not IsAspNetCoreRuntimeInstalled
Filename: "sc"; Parameters: "create {#MyAppServiceName} binPath= ""{app}\{#MyAppExeName}"" start= auto"; Tasks: installservice; Flags: runhidden
Filename: "sc"; Parameters: "description {#MyAppServiceName} ""Система управления сканерами штрихкодов и QR-кодов"""; Tasks: installservice; Flags: runhidden
Filename: "sc"; Parameters: "start {#MyAppServiceName}"; Tasks: installservice; Flags: runhidden

[UninstallRun]
Filename: "sc"; Parameters: "stop {#MyAppServiceName}"; Flags: runhidden
Filename: "sc"; Parameters: "delete {#MyAppServiceName}"; Flags: runhidden

[Code]
function IsAspNetCoreRuntimeInstalled: Boolean;
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
    '/C dotnet --list-runtimes | findstr "AspNetCore" > "' + TempFile + '" 2>&1',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode
  );
  
  if ExecResult and (ResultCode = 0) then
  begin
    Result := LoadStringFromFile(TempFile, Output);
    if Result then
      Result := Pos('{#AspNetCoreVersion}', Output) > 0;
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
  
  Exec('sc', 'stop ' + '{#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc', 'delete ' + '{#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

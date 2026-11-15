; Mostawsef clinic installer (one-PC). Built by scripts/build-setup.ps1
; Requires Administrator. Does not wipe the PostgreSQL data directory.

#define MyAppName "Mostawsef"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Mostawsef"
#define MyAppExeName "Mostawsef.Desktop.exe"
; Staging folder is filled by build-setup.ps1 (Api, Desktop, scripts).
#ifndef MyStagingDir
  #define MyStagingDir "..\..\artifacts\installer-staging"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\..\artifacts\installer"
#endif

[Setup]
AppId={{A7E8C2F1-4B9D-4E2A-9C31-8F0E6D5C4B3A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName=C:\Mostawsef
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#MyOutputDir}
OutputBaseFilename=Mostawsef-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Desktop\{#MyAppExeName}
CloseApplications=force
RestartApplications=no
; Keep clinic machine config across upgrades (backup/restore in [Code] + install-clinic.ps1).
UsePreviousAppDir=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Published apps + helper scripts (staging laid out by build-setup.ps1).
Source: "{#MyStagingDir}\Api\*"; DestDir: "{app}\Api"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "appsettings.Clinic.json"
Source: "{#MyStagingDir}\Desktop\*"; DestDir: "{app}\Desktop"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyStagingDir}\scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Desktop\{#MyAppExeName}"; WorkingDir: "{app}\Desktop"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Desktop\{#MyAppExeName}"; WorkingDir: "{app}\Desktop"; Tasks: desktopicon

[Run]
; After files are in place: Postgres (if needed), DB, connection string, Windows service.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\install-clinic.ps1"" -InstallRoot ""{app}"" -ConfigBackup ""{tmp}\mostawsef-config"""; \
  StatusMsg: "Configuring PostgreSQL and the Mostawsef API service..."; \
  Flags: runhidden waituntilterminated
Filename: "{app}\Desktop\{#MyAppExeName}"; Description: "Launch Mostawsef"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop/remove API service only. Never uninstall PostgreSQL or delete its data directory.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\install-api-service.ps1"" -Uninstall"; \
  Flags: runhidden waituntilterminated; RunOnceId: "RemoveMostawsefApiService"

[Code]
var
  PgPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  PgPage := CreateInputQueryPage(wpSelectTasks,
    'PostgreSQL', 'Database setup',
    'If PostgreSQL 17 is already installed on this PC, enter the postgres superuser password.' #13#10 +
    'Leave blank to install PostgreSQL via winget (new clinic PCs).' #13#10 +
    'On upgrades that already have a connection string, you can leave this blank.');
  PgPage.Add('postgres password:', True);
end;

procedure BackupClinicConfig;
var
  BackupDir, AppJson, ClinicJson: string;
begin
  BackupDir := ExpandConstant('{tmp}\mostawsef-config');
  ForceDirectories(BackupDir);
  AppJson := ExpandConstant('{app}\Api\appsettings.json');
  ClinicJson := ExpandConstant('{app}\Api\appsettings.Clinic.json');
  if FileExists(AppJson) then
    FileCopy(AppJson, BackupDir + '\appsettings.json', False);
  if FileExists(ClinicJson) then
    FileCopy(ClinicJson, BackupDir + '\appsettings.Clinic.json', False);
end;

procedure SavePostgresPasswordIfAny;
var
  SuperPass: string;
begin
  SuperPass := Trim(PgPage.Values[0]);
  if SuperPass <> '' then
  begin
    ForceDirectories(ExpandConstant('{tmp}\mostawsef-config'));
    SaveStringToFile(ExpandConstant('{tmp}\mostawsef-config\postgres-super.password'), SuperPass, False);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Before file extraction: keep JWT + connection string across upgrades. }
  if CurStep = ssInstall then
    BackupClinicConfig;
  { Before [Run] install-clinic.ps1 }
  if CurStep = ssPostInstall then
    SavePostgresPasswordIfAny;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  MsgBox('Uninstall removes Mostawsef app files and the API Windows service.' #13#10 +
    'PostgreSQL and clinic database files are left in place on purpose.',
    mbInformation, MB_OK);
end;

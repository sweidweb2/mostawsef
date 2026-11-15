; Mostawsef clinic installer (one-PC). Built by scripts/build-setup.ps1
; Requires Administrator. Does not wipe the PostgreSQL data directory.

#define MyAppName "Mostawsef"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef MyOutputBaseFilename
  #define MyOutputBaseFilename "Mostawsef-Setup"
#endif
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
OutputBaseFilename={#MyOutputBaseFilename}
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
; Offline PostgreSQL 17 Windows installer (downloaded on the build PC by build-setup.ps1).
Source: "{#MyStagingDir}\redist\*"; DestDir: "{app}\redist"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Desktop\{#MyAppExeName}"; WorkingDir: "{app}\Desktop"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Desktop\{#MyAppExeName}"; WorkingDir: "{app}\Desktop"; Tasks: desktopicon

[Run]
; After files are in place: Postgres (if needed), DB, connection string, Windows service.
; Visible window + log file so a failure is not a silent "success".
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\install-clinic.ps1"" -InstallRoot ""{app}"" -ConfigBackup ""{tmp}\mostawsef-config"""; \
  StatusMsg: "Configuring PostgreSQL and the Mostawsef API service..."; \
  Flags: waituntilterminated
; Unchecked: do not auto-launch after Setup.
Filename: "{app}\Desktop\{#MyAppExeName}"; Description: "Launch Mostawsef"; Flags: nowait postinstall skipifsilent unchecked

[UninstallRun]
; Stop/remove API service only. Never uninstall PostgreSQL or delete its data directory.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\install-api-service.ps1"" -Uninstall"; \
  Flags: runhidden waituntilterminated; RunOnceId: "RemoveMostawsefApiService"

[Code]
var
  PgPage: TInputQueryWizardPage;

function PostgresServiceExists: Boolean;
var
  ResultCode: Integer;
begin
  { sc query returns 0 when the service exists (any state). }
  Result := Exec('sc.exe', 'query postgresql-x64-17', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and (ResultCode = 0);
end;

function ConnectionStringAlreadyConfigured: Boolean;
var
  AppJson: string;
  Content: AnsiString; { LoadStringFromFile requires AnsiString (Inno 6.7+). }
begin
  Result := False;
  AppJson := ExpandConstant('{app}\Api\appsettings.json');
  if not FileExists(AppJson) then
    Exit;
  if not LoadStringFromFile(AppJson, Content) then
    Exit;
  Result := (Pos('ConnectionStrings', Content) > 0) and (Pos('Default', Content) > 0)
    and (Pos('Host=', Content) > 0);
end;

procedure InitializeWizard;
begin
  PgPage := CreateInputQueryPage(wpSelectTasks,
    'PostgreSQL', 'Database setup',
    'If PostgreSQL 17 is already installed, enter the postgres superuser password (required).' #13#10 +
    'Leave blank only on a new PC with no PostgreSQL yet (Setup installs it from the bundled offline installer).' #13#10 +
    'On upgrades that already have a connection string, you can leave this blank.');
  PgPage.Add('postgres password:', True);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = PgPage.ID then
  begin
    if (Trim(PgPage.Values[0]) = '') and PostgresServiceExists and (not ConnectionStringAlreadyConfigured) then
    begin
      MsgBox(
        'PostgreSQL is already installed on this PC, but Mostawsef has no database connection yet.' #13#10 #13#10 +
        'Enter the postgres superuser password to create the Mostawsef database and Windows service.' #13#10 +
        'Leaving it blank will leave the Desktop unable to reach the server.',
        mbError, MB_OK);
      Result := False;
    end;
  end;
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
    CopyFile(AppJson, BackupDir + '\appsettings.json', False);
  if FileExists(ClinicJson) then
    CopyFile(ClinicJson, BackupDir + '\appsettings.Clinic.json', False);
end;

procedure SavePostgresPasswordIfAny;
var
  SuperPass, TmpFile, AppFile: string;
begin
  SuperPass := Trim(PgPage.Values[0]);
  ForceDirectories(ExpandConstant('{tmp}\mostawsef-config'));
  ForceDirectories(ExpandConstant('{app}\scripts'));
  TmpFile := ExpandConstant('{tmp}\mostawsef-config\postgres-super.password');
  AppFile := ExpandConstant('{app}\scripts\postgres-super.password');
  { Always write both paths so install-clinic can find the password even if the temp folder differs. }
  if SuperPass <> '' then
  begin
    SaveStringToFile(TmpFile, SuperPass, False);
    SaveStringToFile(AppFile, SuperPass, False);
  end
  else
  begin
    if FileExists(TmpFile) then
      DeleteFile(TmpFile);
    if FileExists(AppFile) then
      DeleteFile(AppFile);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Before file extraction: keep JWT + connection string across upgrades. }
  if CurStep = ssInstall then
    BackupClinicConfig;
  { Before [Run] install-clinic.ps1 — password must already be on disk. }
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

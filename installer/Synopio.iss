; Version
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppName "Synopio"
#define AppExe "Synopio.exe"
#define AppUrl "https://ckass.de/synopio/"
#define RepositoryUrl "https://github.com/TripleT3005/Synopio"
#define RuntimeUrl "https://dotnet.microsoft.com/download/dotnet/10.0"
#define ProjectExtension ".synopio"
#define ProjectProgId "Synopio.Project"

; Setup
[Setup]
AppId={{6C1F0B7E-3D42-4C8A-9B5E-2F7D8A41C9E3}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=TripleT3005
AppPublisherURL={#AppUrl}
AppSupportURL={#RepositoryUrl}/issues
AppUpdatesURL={#RepositoryUrl}/releases
AppCopyright=Copyright (c) TripleT3005
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
SetupIconFile=..\assets\icons\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir=..\dist
OutputBaseFilename={#AppName}-{#AppVersion}-windows-x64-setup

; Languages
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

; Texts
[CustomMessages]
FileAssociation=Open Synopio project files (.synopio) with Synopio
german.FileAssociation=Synopio-Projektdateien (.synopio) mit Synopio öffnen
ProjectFile=Synopio project
german.ProjectFile=Synopio-Projekt
RuntimeMissing=Synopio needs the .NET 10 Desktop Runtime, which was not found on this computer.%n%nAfter the installation, download it from Microsoft and install it. Open the download page now?
german.RuntimeMissing=Synopio benötigt die .NET 10 Desktop Runtime, die auf diesem Computer nicht gefunden wurde.%n%nLade sie nach der Installation bei Microsoft herunter und installiere sie. Downloadseite jetzt öffnen?

; Tasks
[Tasks]
Name: "fileassociation"; Description: "{cm:FileAssociation}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

; Files
[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\lang\*.lang"; DestDir: "{app}\lang"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-LICENSES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

; Shortcuts
[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

; File association
[Registry]
Root: HKA; Subkey: "Software\Classes\{#ProjectExtension}"; ValueType: string; ValueName: ""; ValueData: "{#ProjectProgId}"; Flags: uninsdeletevalue; Tasks: fileassociation
Root: HKA; Subkey: "Software\Classes\{#ProjectExtension}\OpenWithProgids"; ValueType: string; ValueName: "{#ProjectProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassociation
Root: HKA; Subkey: "Software\Classes\{#ProjectProgId}"; ValueType: string; ValueName: ""; ValueData: "{cm:ProjectFile}"; Flags: uninsdeletekey; Tasks: fileassociation
Root: HKA; Subkey: "Software\Classes\{#ProjectProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"; Tasks: fileassociation
Root: HKA; Subkey: "Software\Classes\{#ProjectProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: fileassociation

; Start
[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; Check: IsRuntimeInstalled

; Runtime check
[Code]
function IsRuntimeInstalled: Boolean;
var
  Versions: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), Versions) then
  begin
    Result := True;
    FindClose(Versions);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorCode: Integer;
begin
  if (CurStep = ssPostInstall) and not IsRuntimeInstalled and not WizardSilent then
  begin
    if MsgBox(CustomMessage('RuntimeMissing'), mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', '{#RuntimeUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

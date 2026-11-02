#ifndef AppVersion
  #error AppVersion must be specified
#endif
#ifndef PublishDir
  #error PublishDir must be specified
#endif
#ifndef ReleaseOutputDir
  #error ReleaseOutputDir must be specified
#endif

[Setup]
AppId={{A6F0ECF1-C3C9-45ED-8136-6E8A1CC048D7}
AppName=MHGU Save Editor Early Access
AppVersion={#AppVersion}
VersionInfoVersion={#AppNumericVersion}
AppPublisher=ramosdalrymple-afk
AppPublisherURL=https://github.com/ramosdalrymple-afk/mhgu-save-editor
AppSupportURL=https://github.com/ramosdalrymple-afk/mhgu-save-editor/issues
AppUpdatesURL=https://github.com/ramosdalrymple-afk/mhgu-save-editor/releases
DefaultDirName={localappdata}\Programs\MHGU Save Editor Early Access
DefaultGroupName=MHGU Save Editor Early Access
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#ReleaseOutputDir}
OutputBaseFilename=MHGU-Save-Editor-{#AppVersion}-win-x64-Setup
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayIcon={app}\MhguSave.App.exe
CloseApplications=yes
RestartApplications=no
InfoBeforeFile={#PublishDir}\EARLY-ACCESS.txt

[Tasks]
Name: "desktopicon"; Description: "Create an Early Access desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MHGU Save Editor Early Access"; Filename: "{app}\MhguSave.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\MHGU Save Editor Early Access"; Filename: "{app}\MhguSave.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\MhguSave.App.exe"; Description: "Launch MHGU Save Editor Early Access"; Flags: nowait postinstall skipifsilent

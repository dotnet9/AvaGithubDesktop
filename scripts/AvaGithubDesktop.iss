; AvaGithubDesktop Windows installer.
; Build from the repository root with Inno Setup 6 and pass /DAppVersion=x.y.z.

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\win-x64\AvaGithubDesktop"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts\release"
#endif

[Setup]
AppId={{D1E2F3A4-B5C6-4D7E-8F9A-0B1C2D3E4F5A}}
AppName=AvaGithubDesktop
AppVersion={#AppVersion}
AppPublisher=Dotnet9
AppPublisherURL=https://github.com/dotnet9/AvaGithubDesktop
AppSupportURL=https://github.com/dotnet9/AvaGithubDesktop/issues
DefaultDirName={autopf}\AvaGithubDesktop
DefaultGroupName=AvaGithubDesktop
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=AvaGithubDesktop-v{#AppVersion}-win-x64-setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\AvaGithubDesktop"; Filename: "{app}\AvaGithubDesktop.exe"
Name: "{autodesktop}\AvaGithubDesktop"; Filename: "{app}\AvaGithubDesktop.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Run]
Filename: "{app}\AvaGithubDesktop.exe"; Description: "{cm:LaunchProgram,AvaGithubDesktop}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

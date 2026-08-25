[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName=Copper IPTV Player
AppVersion=1.0.1
AppPublisher=Mohamed Subarashi
AppPublisherURL=https://github.com/MohamedSubarashi/CopperIPTV
DefaultDirName={autopf}\CopperIPTV
DefaultGroupName=Copper IPTV Player
OutputDir=installer
OutputBaseFilename=CopperIPTV-1.0.1-Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayName=Copper IPTV Player
UninstallDisplayIcon={app}\CopperIPTV.exe
SetupIconFile=..\CopperIPTV\Assets\App.ico
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Copper IPTV Player"; Filename: "{app}\CopperIPTV.exe"
Name: "{group}\Uninstall Copper IPTV Player"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Copper IPTV Player"; Filename: "{app}\CopperIPTV.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CopperIPTV.exe"; Description: "Launch Copper IPTV Player"; Flags: nowait postinstall

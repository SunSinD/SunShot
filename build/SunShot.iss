#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef PackageDir
  #error PackageDir is required
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId=CF467880-6118-4A30-A944-5DAC243C9FCB
AppMutex=CF467880-6118-4A30-A944-5DAC243C9FCB
AppName=SunShot
AppVersion={#AppVersion}
AppPublisher=SunSinD
AppPublisherURL=https://github.com/SunSinD/SunShot
AppSupportURL=https://github.com/SunSinD/SunShot/issues
AppUpdatesURL=https://github.com/SunSinD/SunShot/releases
AppCopyright=ShareX Team; SunShot modifications by SunSinD
DefaultDirName={localappdata}\Programs\SunShot
DefaultGroupName=SunShot
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22621
LicenseFile=..\LICENSE.txt
SetupIconFile=..\SunShot\ShareX_Icon.ico
UninstallDisplayIcon={app}\SunShot.exe
OutputDir={#PackageDir}
OutputBaseFilename=SunShot-{#AppVersion}-setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Portable"

[Icons]
Name: "{userprograms}\SunShot"; Filename: "{app}\SunShot.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\SunShot"; Filename: "{app}\SunShot.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SunShot.exe"; Description: "Open SunShot"; Flags: nowait postinstall; Check: not IsNoRun

[UninstallDelete]
Type: files; Name: "{userstartup}\SunShot.lnk"
Type: files; Name: "{usersendto}\SunShot.lnk"

[Registry]
Root: HKCU; Subkey: "Software\Classes\*\shell\SunShot"; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SunShot"; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\image\shell\SunShotImageEditor"; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.sxcu\OpenWithProgids"; ValueName: "SunShot.sxcu"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SunShot.sxcu"; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.sxie\OpenWithProgids"; ValueName: "SunShot.sxie"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SunShot.sxie"; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\com.sunshot.sunshot"; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Mozilla\NativeMessagingHosts\sunshot"; Flags: dontcreatekey uninsdeletekey

[Code]
function IsNoRun(): Boolean;
var
  i: Integer;
begin
  Result := False;
  for i := 1 to ParamCount do
    if CompareText(ParamStr(i), '/NORUN') = 0 then
      Result := True;
end;

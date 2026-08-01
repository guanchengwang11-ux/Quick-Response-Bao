#define MyAppName "Quick Response Bao"
#define MyAppVersion "1.0.0-rc.1"
#define MyAppPublisher "Quick Response Bao contributors"
#define MyAppExeName "QuickResponseBao.exe"

[Setup]
AppId={{7C09398B-0BC5-4B69-A05A-BC1B04B70960}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Quick Response Bao
DefaultGroupName={#MyAppName}
OutputDir=..\artifacts
OutputBaseFilename=Quick-Response-Bao-Setup-{#MyAppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[CustomMessages]
english.RemoveUserData=Remove the Quick Response Bao user database, settings, backups and logs?%nChoose No to keep user data for reinstall or upgrade.
chinesesimplified.RemoveUserData=是否删除 Quick Response Bao 的话术数据库、设置、备份和日志？%n选择“否”可为重新安装或升级保留用户数据。
english.DesktopIcon=Create a desktop shortcut
chinesesimplified.DesktopIcon=创建桌面快捷方式
english.AdditionalShortcuts=Additional shortcuts:
chinesesimplified.AdditionalShortcuts=附加快捷方式：
english.LaunchApp=Launch Quick Response Bao
chinesesimplified.LaunchApp=启动 Quick Response Bao

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked

[Files]
Source: "..\artifacts\rc-publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RemoveUserData: Boolean;
begin
  if CurUninstallStep <> usPostUninstall then Exit;
  if UninstallSilent then
    RemoveUserData := Pos('/REMOVEUSERDATA', Uppercase(GetCmdTail)) > 0
  else
    RemoveUserData := MsgBox(ExpandConstant('{cm:RemoveUserData}'), mbConfirmation, MB_YESNO) = IDYES;
  if RemoveUserData then
    DelTree(ExpandConstant('{localappdata}\QuickResponseBao'), True, True, True);
end;

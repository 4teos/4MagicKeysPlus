#ifndef AppVersion
  #error Pass the version: ISCC /DAppVersion=<MkpAppVersion> /DPublishDir=<bin\publish>
#endif
#ifndef PublishDir
  #define PublishDir "..\bin\publish"
#endif

[Setup]
AppId={{D2185610-D11C-40C8-BA7C-27386DF96781}
AppName=4MagicKeys+
AppVersion={#AppVersion}
AppVerName=4MagicKeys+ {#AppVersion}
AppPublisher=4teos
AppPublisherURL=https://github.com/4teos/4MagicKeysPlus
AppSupportURL=https://github.com/4teos/4MagicKeysPlus/issues
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\4MagicKeysPlus
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.26100
WizardStyle=modern dynamic
SetupIconFile=..\assets\icon\4MagicKeysPlus.ico
UninstallDisplayIcon={app}\4MagicKeysPlus.exe
UninstallDisplayName=4MagicKeys+
OutputBaseFilename=4MagicKeysPlus-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
CloseApplications=no
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.TestSigningCaption=Test-signing mode
english.TestSigningDescription=The driver needs the test-signing mode of Windows (TESTSIGNING)
english.TestSigningWhy=The 4MagicKeys+ driver is not signed by Microsoft: Setup signs it with a certificate that it creates on this computer during the installation. Windows loads such a driver only in test-signing mode, and that mode is off now.
english.TestSigningWillTurnOn=Setup will turn test-signing mode on. The driver starts working after a restart, until then the keyboard may not respond.%n%nIn test-signing mode Windows loads any driver signed with a test certificate, so the computer is less protected. You use 4MagicKeys+ at your own risk.
english.TestSigningSecureBoot=Secure Boot is on, and Windows does not allow test-signing mode while it is. Turn Secure Boot off in the UEFI (BIOS) settings of the computer, then start Setup again.
english.TestSigningLink=What test-signing mode is and how to turn it off
english.TestSigningMicrosoftLink=Test-signing mode in the Microsoft documentation
english.SecureBootBlocks=Secure Boot is on, so Windows cannot turn on the test-signing mode that the 4MagicKeys+ driver needs. Turn Secure Boot off in the UEFI (BIOS) settings and start Setup again.
english.AutostartTask=Start the 4MagicKeys+ agent with Windows
english.StartAgent=Start the agent
english.OpenApp=Open 4MagicKeys+
english.InstallingDriver=Signing and installing the driver...
english.InstallFailed=The driver or the default settings could not be installed. What happened is in %1.
english.UninstallCaption=Uninstall 4MagicKeys+
english.UninstallText=4MagicKeys+ is removed together with its driver, the signing certificate and the settings of all keyboards.
english.UninstallTestSigning=Turn test-signing mode off (TESTSIGNING)
english.UninstallAllUsers=Remove the settings of all Windows users
english.RemovingDriver=Removing the driver...
english.UninstallFailed=Some parts of 4MagicKeys+ could not be removed. What happened is in %1.
russian.TestSigningCaption=Тестовый режим подписи
russian.TestSigningDescription=Драйверу нужен тестовый режим подписи Windows (TESTSIGNING)
russian.TestSigningWhy=Драйвер 4MagicKeys+ не подписан Microsoft: программа установки подписывает его сертификатом, который она создаёт на этом компьютере во время установки. Такой драйвер Windows загружает только в тестовом режиме подписи, а сейчас этот режим выключен.
russian.TestSigningWillTurnOn=Программа установки включит тестовый режим. Драйвер заработает после перезагрузки, до неё клавиатура может не отвечать.%n%nВ тестовом режиме Windows загружает любой драйвер, подписанный тестовым сертификатом, поэтому компьютер защищён слабее. Вы используете 4MagicKeys+ на свой страх и риск.
russian.TestSigningSecureBoot=Включена безопасная загрузка (Secure Boot), а при ней Windows не разрешает тестовый режим. Выключите Secure Boot в настройках UEFI (BIOS) компьютера и запустите установку снова.
russian.TestSigningLink=Что такое тестовый режим подписи и как его выключить
russian.TestSigningMicrosoftLink=Тестовый режим подписи в документации Microsoft
russian.SecureBootBlocks=Включена безопасная загрузка (Secure Boot), поэтому Windows не может включить тестовый режим подписи, который нужен драйверу 4MagicKeys+. Выключите Secure Boot в настройках UEFI (BIOS) и запустите установку снова.
russian.AutostartTask=Запускать агент 4MagicKeys+ вместе с Windows
russian.StartAgent=Запустить агент
russian.OpenApp=Открыть 4MagicKeys+
russian.InstallingDriver=Подпись и установка драйвера...
russian.InstallFailed=Не удалось установить драйвер или настройки по умолчанию. Подробности в файле %1.
russian.UninstallCaption=Удаление 4MagicKeys+
russian.UninstallText=4MagicKeys+ будет удалён вместе с драйвером, сертификатом подписи и настройками всех клавиатур.
russian.UninstallTestSigning=Выключить тестовый режим подписи (TESTSIGNING)
russian.UninstallAllUsers=Удалить настройки у всех пользователей Windows
russian.RemovingDriver=Удаление драйвера...
russian.UninstallFailed=Часть 4MagicKeys+ удалить не удалось. Подробности в файле %1.

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"

[Files]
Source: "{#PublishDir}\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\driver\*"; DestDir: "{app}\driver"; Flags: ignoreversion
Source: "uninstall.ps1"; DestDir: "{app}\setup"; Flags: ignoreversion
Source: "install.ps1"; DestDir: "{tmp}"
Source: "..\packaging\sign.ps1"; DestDir: "{tmp}"
Source: "..\packaging\signing-key-readme.txt"; DestDir: "{tmp}"

[Icons]
Name: "{autoprograms}\4MagicKeys+"; Filename: "{app}\4MagicKeysPlus.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "4MagicKeysPlusAgent"; ValueData: """{app}\4MagicKeysPlusAgent.exe"""; Tasks: autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: none; ValueName: "4MagicKeysPlusAgent"; Flags: deletevalue; Tasks: autostart

[Run]
Filename: "{app}\4MagicKeysPlusAgent.exe"; Description: "{cm:StartAgent}"; Flags: postinstall nowait runasoriginaluser shellexec
Filename: "{app}\4MagicKeysPlus.exe"; Description: "{cm:OpenApp}"; Flags: postinstall nowait runascurrentuser

[UninstallDelete]
Type: filesandordirs; Name: "{app}\setup"
Type: dirifempty; Name: "{app}"

[Code]
const
  TestSigningWikiUrl = 'https://github.com/4teos/4MagicKeysPlus/wiki/Test-Signing';
  TestSigningMicrosoftUrl = 'https://learn.microsoft.com/windows-hardware/drivers/install/the-testsigning-boot-configuration-option';
  RestartNeededCode = 3010;

var
  TestSigningPage: TWizardPage;
  RestartAfterInstall: Boolean;
  RestartAfterUninstall: Boolean;
  TurnOffTestSigning: Boolean;
  RemoveForAllUsers: Boolean;

{ The options Windows was started with: the BCD setting itself may already be on and still wait for a restart. }
function TestSigningActive: Boolean;
var
  Options: String;
begin
  Result := RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control', 'SystemStartOptions', Options)
    and (Pos('TESTSIGNING', Uppercase(Options)) > 0);
end;

function SecureBootOn: Boolean;
var
  Enabled: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SYSTEM\CurrentControlSet\Control\SecureBoot\State', 'UEFISecureBootEnabled', Enabled)
    and (Enabled = 1);
end;

function TestSigningBlocked: Boolean;
begin
  Result := not TestSigningActive and SecureBootOn;
end;

function PowerShellPath: String;
begin
  Result := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
end;

function RunScript(const Script, Arguments: String): Integer;
begin
  if not Exec(PowerShellPath, '-NoProfile -ExecutionPolicy Bypass -File "' + Script + '" ' + Arguments, '', SW_HIDE, ewWaitUntilTerminated, Result) then
    Result := -1;
end;

procedure OpenLink(Sender: TObject; const Link: String; LinkType: TSysLinkType);
var
  ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', Link, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

function AddText(const Parent: TWinControl; const Text: String; const Top: Integer): TNewStaticText;
begin
  Result := TNewStaticText.Create(Parent);
  Result.Parent := Parent;
  Result.Top := Top;
  Result.Width := Parent.ClientWidth;
  Result.AutoSize := False;
  Result.WordWrap := True;
  Result.Caption := Text;
  Result.AdjustHeight;
end;

function AddLink(const Parent: TWinControl; const Url, Text: String; const Top: Integer): TNewLinkLabel;
begin
  Result := TNewLinkLabel.Create(Parent);
  Result.Parent := Parent;
  Result.Top := Top;
  Result.Caption := '<a href="' + Url + '">' + Text + '</a>';
  Result.OnLinkClick := @OpenLink;
end;

procedure CreateTestSigningPage;
var
  Text: TNewStaticText;
  Link: TNewLinkLabel;
begin
  TestSigningPage := CreateCustomPage(wpWelcome, CustomMessage('TestSigningCaption'), CustomMessage('TestSigningDescription'));
  Text := AddText(TestSigningPage.Surface, CustomMessage('TestSigningWhy'), 0);
  if TestSigningBlocked then
    Text := AddText(TestSigningPage.Surface, CustomMessage('TestSigningSecureBoot'), Text.Top + Text.Height + ScaleY(12))
  else
    Text := AddText(TestSigningPage.Surface, CustomMessage('TestSigningWillTurnOn'), Text.Top + Text.Height + ScaleY(12));

  Link := AddLink(TestSigningPage.Surface, TestSigningWikiUrl, CustomMessage('TestSigningLink'), Text.Top + Text.Height + ScaleY(12));
  AddLink(TestSigningPage.Surface, TestSigningMicrosoftUrl, CustomMessage('TestSigningMicrosoftLink'), Link.Top + Link.Height + ScaleY(6));
end;

procedure InitializeWizard;
begin
  CreateTestSigningPage;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = TestSigningPage.ID) and TestSigningActive;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = TestSigningPage.ID then
    WizardForm.NextButton.Enabled := not TestSigningBlocked;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  if TestSigningBlocked then begin
    Result := CustomMessage('SecureBootBlocks');
    exit;
  end;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM 4MagicKeysPlusAgent.exe /IM 4MagicKeysPlus.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure InstallDriver;
var
  Arguments: String;
  ResultCode: Integer;
begin
  WizardForm.StatusLabel.Caption := CustomMessage('InstallingDriver');
  Arguments := '-AppFolder "' + ExpandConstant('{app}') + '"';
  if not TestSigningActive then
    Arguments := Arguments + ' -TurnOnTestSigning';

  ResultCode := RunScript(ExpandConstant('{tmp}\install.ps1'), Arguments);
  if ResultCode = RestartNeededCode then
    RestartAfterInstall := True
  else if ResultCode <> 0 then
    SuppressibleMsgBox(FmtMessage(CustomMessage('InstallFailed'), [ExpandConstant('{app}\setup\install.log')]), mbError, MB_OK, IDOK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallDriver;
end;

function NeedRestart: Boolean;
begin
  Result := RestartAfterInstall;
end;

function AskUninstallOptions: Boolean;
var
  Form: TSetupForm;
  Text: TNewStaticText;
  TestSigningBox, AllUsersBox: TNewCheckBox;
  OKButton, CancelButton: TNewButton;
  LineProbe: TNewStaticText;
  Margin, BoxHeight: Integer;
begin
  Margin := ScaleX(16);
  Form := CreateCustomForm(ScaleX(300), ScaleY(200), True, True);
  try
    Form.Caption := CustomMessage('UninstallCaption');

    Text := TNewStaticText.Create(Form);
    Text.Parent := Form;
    Text.Left := Margin;
    Text.Top := Margin;
    Text.Width := Form.ClientWidth - 2 * Margin;
    Text.AutoSize := False;
    Text.WordWrap := True;
    Text.Caption := CustomMessage('UninstallText');
    Text.AdjustHeight;

    { A check box keeps a fixed height that clips its caption in the dynamic style's font. }
    LineProbe := TNewStaticText.Create(Form);
    LineProbe.Parent := Form;
    LineProbe.Visible := False;
    LineProbe.Caption := 'Ag';
    BoxHeight := LineProbe.Height + ScaleY(6);

    TestSigningBox := TNewCheckBox.Create(Form);
    TestSigningBox.Parent := Form;
    TestSigningBox.Left := Margin;
    TestSigningBox.Top := Text.Top + Text.Height + ScaleY(16);
    TestSigningBox.Width := Text.Width;
    TestSigningBox.Height := BoxHeight;
    TestSigningBox.Caption := CustomMessage('UninstallTestSigning');

    AllUsersBox := TNewCheckBox.Create(Form);
    AllUsersBox.Parent := Form;
    AllUsersBox.Left := Margin;
    AllUsersBox.Top := TestSigningBox.Top + TestSigningBox.Height + ScaleY(10);
    AllUsersBox.Width := Text.Width;
    AllUsersBox.Height := BoxHeight;
    AllUsersBox.Caption := CustomMessage('UninstallAllUsers');

    OKButton := TNewButton.Create(Form);
    OKButton.Parent := Form;
    OKButton.Caption := SetupMessage(msgButtonOK);
    OKButton.ModalResult := mrOk;
    OKButton.Default := True;

    CancelButton := TNewButton.Create(Form);
    CancelButton.Parent := Form;
    CancelButton.Caption := SetupMessage(msgButtonCancel);
    CancelButton.ModalResult := mrCancel;
    CancelButton.Cancel := True;

    OKButton.Width := Form.CalculateButtonWidth([OKButton.Caption, CancelButton.Caption]);
    CancelButton.Width := OKButton.Width;
    OKButton.Height := ScaleY(23);
    CancelButton.Height := ScaleY(23);
    Form.ClientHeight := AllUsersBox.Top + AllUsersBox.Height + ScaleY(24) + OKButton.Height + Margin;
    CancelButton.Left := Form.ClientWidth - Margin - CancelButton.Width;
    OKButton.Left := CancelButton.Left - ScaleX(6) - OKButton.Width;
    OKButton.Top := Form.ClientHeight - Margin - OKButton.Height;
    CancelButton.Top := OKButton.Top;

    Form.ActiveControl := OKButton;
    Result := Form.ShowModal = mrOk;
    TurnOffTestSigning := TestSigningBox.Checked;
    RemoveForAllUsers := AllUsersBox.Checked;
  finally
    Form.Free;
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := UninstallSilent or AskUninstallOptions;
end;

procedure RemoveDriver;
var
  Arguments: String;
  ResultCode: Integer;
begin
  UninstallProgressForm.StatusLabel.Caption := CustomMessage('RemovingDriver');
  Arguments := '';
  if TurnOffTestSigning then
    Arguments := Arguments + ' -TurnOffTestSigning';
  if RemoveForAllUsers then
    Arguments := Arguments + ' -AllUsers';

  ResultCode := RunScript(ExpandConstant('{app}\setup\uninstall.ps1'), Arguments);
  if ResultCode = RestartNeededCode then
    RestartAfterUninstall := True
  else if ResultCode <> 0 then
    SuppressibleMsgBox(FmtMessage(CustomMessage('UninstallFailed'), [ExpandConstant('{%TEMP}\4MagicKeysPlus-uninstall.log')]), mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveDriver;
end;

function UninstallNeedRestart: Boolean;
begin
  Result := RestartAfterUninstall;
end;

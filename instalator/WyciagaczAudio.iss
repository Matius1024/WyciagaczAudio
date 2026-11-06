; Instalator programu „Wyciągacz audio” (Inno Setup 6.1 lub nowszy).
; Budowanie: w głównym folderze projektu uruchom zbuduj-instalator.ps1.
;
; Instalator:
;  - domyślnie instaluje dla bieżącego użytkownika, bez uprawnień administratora
;    (%LOCALAPPDATA%\Programs\Wyciągacz audio),
;  - sprawdza, czy jest .NET 10 Desktop Runtime x64 oraz ffmpeg i ffprobe,
;  - gdy czegoś brakuje, pokazuje zadania „Pobierz i zainstaluj …” (domyślnie zaznaczone),
;  - .NET pobiera z serwerów Microsoftu i instaluje po cichu (instalator .NET sam poprosi
;    o uprawnienia administratora), ffmpeg pobiera z gyan.dev i kopiuje ffmpeg.exe
;    oraz ffprobe.exe do folderu programu.
;
; Parametry wiersza poleceń do testów:
;  /WYMUSFFMPEG  traktuje ffmpeg jako brakujący, nawet jeśli jest w PATH lub w folderze
;                programu (pozwala przetestować pobieranie ffmpeg na komputerze, który go ma).
;  /BEZNET       nigdy nie pobiera ani nie instaluje .NET, nawet gdy go brakuje.
; Przykład testu cichego:
;  WyciagaczAudio-1.1-instalator.exe /VERYSILENT /SUPPRESSMSGBOXES /CURRENTUSER /WYMUSFFMPEG /BEZNET /DIR="C:\test" /LOG="C:\test.log"

#define AppVer "1.1"
#define AppNazwa "Wyciągacz audio"
#define DotNetUrl "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
#define FfmpegUrl "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
#define DotNetRecznie "https://dotnet.microsoft.com/download/dotnet/10.0"

[Setup]
AppId={{20B592E0-E78C-4C25-A64D-7F84EE4043BB}
AppName={#AppNazwa}
AppVersion={#AppVer}
AppVerName={#AppNazwa} {#AppVer}
AppPublisher=Matius1024
AppPublisherURL=https://github.com/Matius1024/WyciagaczAudio
AppSupportURL=https://github.com/Matius1024/WyciagaczAudio
AppUpdatesURL=https://github.com/Matius1024/WyciagaczAudio/releases
VersionInfoVersion={#AppVer}.0.0
VersionInfoDescription=Instalator programu {#AppNazwa}
DefaultDirName={autopf}\{#AppNazwa}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayName={#AppNazwa} {#AppVer}
UninstallDisplayIcon={app}\WyciagaczAudio.exe
UsedUserAreasWarning=no
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir=..\wydania
OutputBaseFilename=WyciagaczAudio-{#AppVer}-instalator

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "pulpit"; Description: "Skrót na pulpicie"; GroupDescription: "Skróty:"; Flags: unchecked
Name: "wyslijdo"; Description: "Dodaj do menu „Wyślij do” (otwiera plik lub folder w programie)"; GroupDescription: "Skróty:"
Name: "dotnet"; Description: "Pobierz i zainstaluj .NET 10 Desktop Runtime (ok. 60 MB, z serwerów Microsoftu)"; GroupDescription: "Brakujące komponenty (bez nich program nie zadziała):"; Check: PotrzebnyDotNet
Name: "ffmpeg"; Description: "Pobierz ffmpeg (ok. 115 MB, z gyan.dev) i umieść go w folderze programu"; GroupDescription: "Brakujące komponenty (bez nich program nie zadziała):"; Check: PotrzebnyFfmpeg

[Files]
Source: "..\wydania\WyciagaczAudio-{#AppVer}.exe"; DestDir: "{app}"; DestName: "WyciagaczAudio.exe"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppNazwa}"; Filename: "{app}\WyciagaczAudio.exe"; Comment: "Wyciąga ścieżki dźwiękowe z plików wideo"
Name: "{autodesktop}\{#AppNazwa}"; Filename: "{app}\WyciagaczAudio.exe"; Tasks: pulpit
Name: "{usersendto}\{#AppNazwa}"; Filename: "{app}\WyciagaczAudio.exe"; Tasks: wyslijdo

[Run]
Filename: "{app}\WyciagaczAudio.exe"; Description: "Uruchom {#AppNazwa}"; Flags: postinstall nowait skipifsilent; Check: DotNetJest

[UninstallDelete]
; ffmpeg i ffprobe położone w folderze programu przez instalator
Type: files; Name: "{app}\ffmpeg.exe"
Type: files; Name: "{app}\ffprobe.exe"
Type: dirifempty; Name: "{app}"

[Code]
var
  DownloadPage: TDownloadWizardPage;
  DotNetPobrany, FfmpegPobrany: Boolean;
  DotNetProbowano, FfmpegProbowano: Boolean;
  PotrzebnyRestart: Boolean;

{ ---------- parametry wiersza poleceń ---------- }

function JestParametr(const Nazwa: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Nazwa) = 0 then begin
      Result := True;
      Exit;
    end;
end;

function WymusFfmpeg: Boolean;
begin
  Result := JestParametr('/WYMUSFFMPEG');
end;

function BezNet: Boolean;
begin
  Result := JestParametr('/BEZNET');
end;

{ ---------- wykrywanie .NET 10 Desktop Runtime ---------- }

function WersjaDotNet: String;
var
  FR: TFindRec;
  Nazwy: TArrayOfString;
  I: Integer;
begin
  Result := '';
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FR) then
  try
    repeat
      if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        Result := FR.Name;
    until not FindNext(FR);
  finally
    FindClose(FR);
  end;
  if Result <> '' then
    Exit;
  { Zapasowo: wpis instalatora .NET w rejestrze (widok 32-bitowy) i istniejący dotnet.exe }
  if FileExists(ExpandConstant('{commonpf64}\dotnet\dotnet.exe')) and
     RegGetValueNames(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Nazwy) then
    for I := 0 to GetArrayLength(Nazwy) - 1 do
      if Copy(Nazwy[I], 1, 3) = '10.' then
        Result := Nazwy[I];
end;

function DotNetJest: Boolean;
begin
  Result := WersjaDotNet <> '';
end;

function PotrzebnyDotNet: Boolean;
begin
  Result := (not BezNet) and (not DotNetJest);
end;

{ ---------- wykrywanie ffmpeg i ffprobe ---------- }

function RozwinZmienne(S: String): String;
var
  P, Q: Integer;
begin
  Result := '';
  while True do begin
    P := Pos('%', S);
    if P = 0 then
      Break;
    Q := Pos('%', Copy(S, P + 1, Length(S)));
    if Q = 0 then
      Break;
    Result := Result + Copy(S, 1, P - 1) + GetEnv(Copy(S, P + 1, Q - 1));
    S := Copy(S, P + Q + 1, Length(S));
  end;
  Result := Result + S;
end;

function MaFfmpeg(Katalog: String): Boolean;
begin
  Katalog := AddBackslash(Katalog);
  Result := FileExists(Katalog + 'ffmpeg.exe') and FileExists(Katalog + 'ffprobe.exe');
end;

function SzukajWLiscie(Lista: String): String;
var
  P: Integer;
  D: String;
begin
  Result := '';
  Lista := Lista + ';';
  while Lista <> '' do begin
    P := Pos(';', Lista);
    D := Trim(Copy(Lista, 1, P - 1));
    Delete(Lista, 1, P);
    StringChangeEx(D, '"', '', True);
    D := RozwinZmienne(D);
    if (D <> '') and MaFfmpeg(D) then begin
      Result := RemoveBackslashUnlessRoot(D);
      Exit;
    end;
  end;
end;

{ Zwraca katalog z ffmpeg.exe i ffprobe.exe albo pusty tekst. KatalogProgramu: folder docelowy. }
function ZnajdzFfmpeg(KatalogProgramu: String): String;
var
  S: String;
begin
  Result := '';
  if WymusFfmpeg then
    Exit;
  if (KatalogProgramu <> '') and MaFfmpeg(KatalogProgramu) then begin
    Result := KatalogProgramu;
    Exit;
  end;
  Result := SzukajWLiscie(GetEnv('PATH'));
  if Result <> '' then
    Exit;
  if RegQueryStringValue(HKCU, 'Environment', 'Path', S) then
    Result := SzukajWLiscie(S);
  if Result <> '' then
    Exit;
  if RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', S) then
    Result := SzukajWLiscie(S);
end;

function PotrzebnyFfmpeg: Boolean;
begin
  Result := ZnajdzFfmpeg(WizardDirValue) = '';
end;

{ ---------- kreator ---------- }

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('Pobieranie komponentów',
    'Instalator pobiera brakujące komponenty. Można przerwać przyciskiem Przerwij.', nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
  Log('Wykrywanie: .NET 10 Desktop Runtime = ' + WersjaDotNet);
  if BezNet then
    Log('Parametr /BEZNET: instalacja .NET zostanie pominięta.');
  if WymusFfmpeg then
    Log('Parametr /WYMUSFFMPEG: ffmpeg jest traktowany jako brakujący.');
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  Wersja, Katalog: String;
begin
  Result := MemoDirInfo + NewLine + NewLine;
  if MemoTasksInfo <> '' then
    Result := Result + MemoTasksInfo + NewLine + NewLine;
  Result := Result + 'Stan komponentów:' + NewLine;

  Wersja := WersjaDotNet;
  if Wersja <> '' then
    Result := Result + Space + '.NET 10 Desktop Runtime: zainstalowany (wersja ' + Wersja + ')'
  else if WizardIsTaskSelected('dotnet') then
    Result := Result + Space + '.NET 10 Desktop Runtime: brak, zostanie pobrany i zainstalowany'
  else if BezNet then
    Result := Result + Space + '.NET 10 Desktop Runtime: brak, pominięty (parametr /BEZNET)'
  else
    Result := Result + Space + '.NET 10 Desktop Runtime: brak, nie zostanie zainstalowany. Bez niego program się nie uruchomi';
  Result := Result + NewLine;

  Katalog := ZnajdzFfmpeg(WizardDirValue);
  if Katalog <> '' then
    Result := Result + Space + 'ffmpeg: znaleziony w ' + Katalog
  else if WizardIsTaskSelected('ffmpeg') then
    Result := Result + Space + 'ffmpeg: brak, zostanie pobrany do folderu programu'
  else
    Result := Result + Space + 'ffmpeg: brak, nie zostanie pobrany. Można go zainstalować później poleceniem winget install Gyan.FFmpeg';
  Result := Result + NewLine;
end;

{ Pobiera jeden plik przez stronę pobierania. Zwraca True, gdy się udało. }
function PobierzNaStronie(const Url, Nazwa, Opis: String; var Przerwano: Boolean): Boolean;
begin
  Result := False;
  DownloadPage.Clear;
  DownloadPage.Add(Url, Nazwa, '');
  try
    DownloadPage.Download;
    Result := True;
    Log('Pobrano ' + Opis + ' do ' + ExpandConstant('{tmp}\') + Nazwa);
  except
    if DownloadPage.AbortedByUser then begin
      Przerwano := True;
      Log('Pobieranie przerwane przez użytkownika: ' + Opis);
    end else
      Log('Błąd pobierania ' + Opis + ': ' + GetExceptionMessage);
  end;
end;

procedure KomunikatDotNetNiePobrany(const Powod: String);
begin
  SuppressibleMsgBox('.NET 10 Desktop Runtime nie został pobrany (' + Powod + ').' + #13#10#13#10 +
    'Program zostanie zainstalowany, ale bez .NET się nie uruchomi. Uruchom instalator ponownie ' +
    'albo pobierz .NET ręcznie: {#DotNetRecznie} (wybierz .NET Desktop Runtime, wersja x64).',
    mbError, MB_OK, IDOK);
end;

procedure KomunikatFfmpegNiePobrany(const Powod: String);
begin
  SuppressibleMsgBox('Nie udało się przygotować ffmpeg (' + Powod + ').' + #13#10#13#10 +
    'Program zostanie zainstalowany. ffmpeg można doinstalować później poleceniem ' +
    'winget install Gyan.FFmpeg albo uruchamiając ten instalator ponownie.',
    mbError, MB_OK, IDOK);
end;

{ Tryb z oknami: pobieranie na stronie z paskiem postępu po kliknięciu „Instaluj”. }
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Przerwano: Boolean;
begin
  Result := True;
  if (CurPageID <> wpReady) or WizardSilent then
    Exit;
  if not (WizardIsTaskSelected('dotnet') or WizardIsTaskSelected('ffmpeg')) then
    Exit;
  Przerwano := False;
  DownloadPage.Show;
  try
    if WizardIsTaskSelected('dotnet') then begin
      DotNetProbowano := True;
      DotNetPobrany := PobierzNaStronie('{#DotNetUrl}', 'dotnet-desktop-runtime.exe', '.NET 10 Desktop Runtime', Przerwano);
      if not DotNetPobrany then begin
        if Przerwano then
          KomunikatDotNetNiePobrany('pobieranie przerwano')
        else
          KomunikatDotNetNiePobrany('błąd pobierania');
      end;
    end;
    if WizardIsTaskSelected('ffmpeg') then begin
      FfmpegProbowano := True;
      if Przerwano then
        KomunikatFfmpegNiePobrany('pobieranie przerwano')
      else begin
        FfmpegPobrany := PobierzNaStronie('{#FfmpegUrl}', 'ffmpeg.zip', 'ffmpeg', Przerwano);
        if not FfmpegPobrany then begin
          if Przerwano then
            KomunikatFfmpegNiePobrany('pobieranie przerwano')
          else
            KomunikatFfmpegNiePobrany('błąd pobierania');
        end;
      end;
    end;
  finally
    DownloadPage.Hide;
  end;
end;

{ Tryb cichy: pobieranie bez okien (DownloadTemporaryFile). }
function PostepPobierania(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

function PobierzPoCichu(const Url, Nazwa, Opis: String): Boolean;
begin
  Result := False;
  try
    DownloadTemporaryFile(Url, Nazwa, '', @PostepPobierania);
    Result := True;
    Log('Pobrano ' + Opis + ' do ' + ExpandConstant('{tmp}\') + Nazwa);
  except
    Log('Błąd pobierania ' + Opis + ': ' + GetExceptionMessage);
  end;
end;

procedure UstawStan(const Tekst: String);
begin
  WizardForm.StatusLabel.Caption := Tekst;
  WizardForm.FilenameLabel.Caption := '';
  Log(Tekst);
end;

procedure ZainstalujDotNet;
var
  Plik: String;
  Kod: Integer;
  Ok: Boolean;
begin
  Plik := ExpandConstant('{tmp}\dotnet-desktop-runtime.exe');
  UstawStan('Instalowanie .NET 10 Desktop Runtime… (może pojawić się pytanie o uprawnienia administratora)');
  Ok := Exec(Plik, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, Kod);
  if (not Ok) and (Kod = 740) then
    { Wymagane podniesienie uprawnień: uruchom przez ShellExec, który pokaże okno UAC. }
    Ok := ShellExec('', Plik, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, Kod);
  DeleteFile(Plik);
  if not Ok then begin
    Log('Nie udało się uruchomić instalatora .NET, kod błędu systemu ' + IntToStr(Kod));
    SuppressibleMsgBox('Nie udało się uruchomić instalatora .NET 10 Desktop Runtime (kod ' + IntToStr(Kod) + ').' + #13#10#13#10 +
      'Program zostanie zainstalowany, ale bez .NET się nie uruchomi. Pobierz .NET ręcznie: {#DotNetRecznie} ' +
      '(wybierz .NET Desktop Runtime, wersja x64).', mbError, MB_OK, IDOK);
    Exit;
  end;
  Log('Instalator .NET zakończył się kodem ' + IntToStr(Kod));
  if (Kod = 0) or (Kod = 1638) or (Kod = 3010) then begin
    if Kod = 3010 then begin
      PotrzebnyRestart := True;
      Log('.NET wymaga ponownego uruchomienia komputera.');
    end;
    Log('Ponowne sprawdzenie: .NET 10 Desktop Runtime = ' + WersjaDotNet);
  end else
    SuppressibleMsgBox('Instalacja .NET 10 Desktop Runtime nie powiodła się (kod ' + IntToStr(Kod) + ').' + #13#10#13#10 +
      'Program zostanie zainstalowany, ale bez .NET się nie uruchomi. Pobierz .NET ręcznie: {#DotNetRecznie} ' +
      '(wybierz .NET Desktop Runtime, wersja x64).', mbError, MB_OK, IDOK);
end;

{ Szuka w rozpakowanym archiwum katalogu bin z ffmpeg.exe i ffprobe.exe. }
function ZnajdzBin(const Katalog: String): String;
var
  FR: TFindRec;
begin
  Result := '';
  if MaFfmpeg(Katalog + '\bin') then begin
    Result := Katalog + '\bin';
    Exit;
  end;
  if FindFirst(Katalog + '\ffmpeg-*', FR) then
  try
    repeat
      if ((FR.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and MaFfmpeg(Katalog + '\' + FR.Name + '\bin') then begin
        Result := Katalog + '\' + FR.Name + '\bin';
        Exit;
      end;
    until not FindNext(FR);
  finally
    FindClose(FR);
  end;
end;

procedure RozpakujFfmpeg;
var
  Zip, Katalog, Bin, App: String;
  Kod: Integer;
begin
  Zip := ExpandConstant('{tmp}\ffmpeg.zip');
  Katalog := ExpandConstant('{tmp}\ffmpeg-rozpakowany');
  App := ExpandConstant('{app}');
  UstawStan('Rozpakowywanie ffmpeg…');
  ForceDirectories(Katalog);
  if Exec(ExpandConstant('{sys}\tar.exe'), '-xf "' + Zip + '" -C "' + Katalog + '"', '', SW_HIDE, ewWaitUntilTerminated, Kod) then
    Log('tar zakończył się kodem ' + IntToStr(Kod))
  else
    Log('Nie udało się uruchomić tar.exe, kod ' + IntToStr(Kod));
  Bin := ZnajdzBin(Katalog);
  if Bin = '' then begin
    Log('tar nie rozpakował ffmpeg, próba przez PowerShell Expand-Archive.');
    Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath ''' + Zip +
      ''' -DestinationPath ''' + Katalog + ''' -Force"', '', SW_HIDE, ewWaitUntilTerminated, Kod);
    Log('PowerShell zakończył się kodem ' + IntToStr(Kod));
    Bin := ZnajdzBin(Katalog);
  end;
  if Bin = '' then
    KomunikatFfmpegNiePobrany('nie udało się rozpakować archiwum')
  else begin
    UstawStan('Kopiowanie ffmpeg do folderu programu…');
    if CopyFile(Bin + '\ffmpeg.exe', App + '\ffmpeg.exe', False) and
       CopyFile(Bin + '\ffprobe.exe', App + '\ffprobe.exe', False) then
      Log('Skopiowano ffmpeg.exe i ffprobe.exe do ' + App)
    else
      KomunikatFfmpegNiePobrany('nie udało się skopiować plików do folderu programu');
  end;
  DelTree(Katalog, True, True, True);
  DeleteFile(Zip);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    Log('Stan ffmpeg przed instalacją: ' + ZnajdzFfmpeg(ExpandConstant('{app}')));
  if CurStep <> ssPostInstall then
    Exit;

  { W trybie cichym strona pobierania nie jest pokazywana, więc pobieramy tutaj. }
  if WizardIsTaskSelected('dotnet') and not DotNetProbowano then begin
    DotNetProbowano := True;
    UstawStan('Pobieranie .NET 10 Desktop Runtime…');
    DotNetPobrany := PobierzPoCichu('{#DotNetUrl}', 'dotnet-desktop-runtime.exe', '.NET 10 Desktop Runtime');
    if not DotNetPobrany then
      KomunikatDotNetNiePobrany('błąd pobierania');
  end;
  if WizardIsTaskSelected('ffmpeg') and not FfmpegProbowano then begin
    FfmpegProbowano := True;
    UstawStan('Pobieranie ffmpeg…');
    FfmpegPobrany := PobierzPoCichu('{#FfmpegUrl}', 'ffmpeg.zip', 'ffmpeg');
    if not FfmpegPobrany then
      KomunikatFfmpegNiePobrany('błąd pobierania');
  end;

  if DotNetPobrany then
    ZainstalujDotNet;
  if FfmpegPobrany then
    RozpakujFfmpeg;

  Log('Stan końcowy: .NET 10 Desktop Runtime = ' + WersjaDotNet + '; ffmpeg w folderze programu = ' +
    IntToStr(Ord(MaFfmpeg(ExpandConstant('{app}')))) + '; ffmpeg znaleziony w = ' + ZnajdzFfmpeg(ExpandConstant('{app}')));
end;

function NeedRestart: Boolean;
begin
  Result := PotrzebnyRestart;
end;

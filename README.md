# Wyciągacz audio

Program wyciąga ścieżki dźwiękowe z plików wideo (mp4, mkv, m4v, mov, ts, avi i inne),
przede wszystkim polską ścieżkę z audiodeskrypcją. Działa z pojedynczym filmem i z całym folderem (np. serialem).

Wersja 1.0. Uruchamianie: `D:\WyciagaczAudio\WyciagaczAudio.exe`. Wymaga ffmpeg i ffprobe w PATH
(`winget install Gyan.FFmpeg`) albo obok pliku exe. Można też przekazać ścieżkę pliku lub folderu
jako argument, np. przez skrót w menu „Wyślij do”.

## Obsługa klawiaturą

- Alt+I – wybierz plik, Alt+F – wybierz folder, Alt+P – uwzględnij podfoldery
- Alt+A – lista ścieżek audio; strzałki przechodzą, spacja zaznacza lub odznacza
- Alt+O – odsłuchaj minutową próbkę podświetlonej ścieżki (od 20% długości filmu), w domyślnym odtwarzaczu
- Alt+R – format wyjściowy, Alt+S – zmiksuj do stereo (tylko przy konwersji)
- Alt+J – jakość dla MP3 i AAC: od 64 do 320 kb/s, domyślnie 192 kb/s (przy AAC 5.1 przepływność jest podwajana)
- Alt+D – folder docelowy (można wpisać własny), Alt+B – wybierz go w oknie
- Alt+N – nadpisuj istniejące pliki (domyślnie istniejące są pomijane)
- Alt+W lub Enter – wyciągnij audio, Alt+L lub Escape – anuluj
- Alt+K – dziennik
- pole „Źródło” (tylko do odczytu) jest pierwsze w kolejności Tab

Komunikaty o stanie są czytane przez NVDA automatycznie, po zakończeniu pojawia się okno z podsumowaniem.

## Jak działa

- Audiodeskrypcja jest rozpoznawana po oznaczeniu w pliku (flaga „dla niedowidzących”) albo po nazwie ścieżki
  (np. „Audiodeskrypcja”, „Audio Description”, „AD”). Jest zaznaczana automatycznie i trafia na górę listy.
- Gdy nic nie jest oznaczone, a są dwie polskie ścieżki, zaznaczana jest pierwsza – audiodeskrypcja to zwykle druga;
  warto odsłuchać próbkę.
- Przy folderze ścieżki są dopasowywane między odcinkami po języku i nazwie, nie po numerze.
- Wynik trafia do folderu obok źródła z dopiskiem, np. `Serial` → `Serial audiodeskrypcja`
  (albo `Serial audio polski`, gdy wybrano zwykłą polską ścieżkę). Podfoldery (sezony) są odtwarzane,
  pliki mają te same nazwy co filmy. Przy kilku ścieżkach z jednego filmu do nazwy dopisywany jest opis ścieżki.
- „Bez konwersji” kopiuje dźwięk 1:1 (AAC → .m4a, AC-3 → .ac3, E-AC-3 → .eac3, inne → odpowiedni format lub .mka).

Kolejne wersje są publikowane w wydaniach (Releases) na GitHubie:
https://github.com/Matius1024/WyciagaczAudio/releases

## Pobieranie

Najnowsza wersja programu (jeden plik exe):
https://github.com/Matius1024/WyciagaczAudio/releases/latest/download/WyciagaczAudio.exe

## Instalator

Plik `WyciagaczAudio-1.0-instalator.exe` instaluje program razem z brakującymi komponentami.

- Instaluje dla bieżącego użytkownika, bez uprawnień administratora, do folderu
  `%LOCALAPPDATA%\Programs\Wyciągacz audio`. Na początku można wybrać instalację dla wszystkich
  użytkowników (wtedy do `Program Files`, z uprawnieniami administratora).
- Zawsze tworzy skrót w menu Start. Na stronie zadań można zaznaczyć:
  - „Skrót na pulpicie” (domyślnie wyłączony),
  - „Dodaj do menu Wyślij do” (domyślnie włączony). Wtedy „Wyślij do”, „Wyciągacz audio” na pliku lub folderze
    (np. serialu) od razu otwiera go w programie.
- Sprawdza, czy jest .NET 10 Desktop Runtime x64 (folder `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\10.*`
  albo wpis w rejestrze) oraz czy `ffmpeg.exe` i `ffprobe.exe` są w folderze programu lub w którymś katalogu ze zmiennej Path
  (także Path użytkownika i systemu, więc ffmpeg z `winget install Gyan.FFmpeg` jest wykrywany).
- Gdy czegoś brakuje, na stronie zadań pojawia się grupa „Brakujące komponenty” z zaznaczonymi polami:
  - „Pobierz i zainstaluj .NET 10 Desktop Runtime” – pobiera z serwerów Microsoftu
    (https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe, ok. 60 MB) i instaluje po cichu;
    instalator .NET sam poprosi o uprawnienia administratora,
  - „Pobierz ffmpeg” – pobiera z gyan.dev (https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip, ok. 115 MB)
    i kopiuje `ffmpeg.exe` oraz `ffprobe.exe` do folderu programu.
- Strona „Gotowy do instalacji” podaje stan komponentów, np. „.NET 10 Desktop Runtime: zainstalowany”,
  „ffmpeg: znaleziony w …”.
- Pobieranie pokazuje pasek postępu i można je przerwać. Gdy pobieranie się nie uda, program i tak zostanie zainstalowany,
  a komunikat podpowie, jak doinstalować brakujący komponent później.
- Odinstalowanie: Ustawienia, Aplikacje, Zainstalowane aplikacje, „Wyciągacz audio 1.0”, Odinstaluj
  (albo `unins000.exe` w folderze programu). Usuwa program, skróty, wpis „Wyślij do” oraz ffmpeg położony w folderze programu.
  .NET zostaje w systemie.
- Parametry do testów: `/WYMUSFFMPEG` traktuje ffmpeg jako brakujący, `/BEZNET` nigdy nie instaluje .NET.
  Działa też tryb cichy, np. `/VERYSILENT /SUPPRESSMSGBOXES /CURRENTUSER`.

## Wymagania

- Windows 10 lub 11, 64-bitowy (x64).
- .NET 10 Desktop Runtime: https://dotnet.microsoft.com/download/dotnet/10.0 – na stronie wybierz „.NET Desktop Runtime”, wersję x64.
- ffmpeg i ffprobe: `winget install Gyan.FFmpeg` albo pliki `ffmpeg.exe` i `ffprobe.exe` położone obok programu.

## Budowanie ze źródeł

Potrzebny jest .NET 10 SDK. W folderze projektu:

```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Gotowy plik `WyciagaczAudio.exe` pojawi się w folderze `publish`.

### Instalator

Potrzebny jest Inno Setup 6.1 lub nowszy. Skopiuj zbudowany program jako `wydania\WyciagaczAudio-1.0.exe`,
a potem w folderze projektu uruchom:

```
powershell -ExecutionPolicy Bypass -File .\zbuduj-instalator.ps1
```

Skrypt instalatora to `instalator\WyciagaczAudio.iss` (wersja w linii `#define AppVer`).
Wynik: `wydania\WyciagaczAudio-1.0-instalator.exe`.

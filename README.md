# Wyciągacz audio

Program wyciąga ścieżki dźwiękowe z plików wideo (mp4, mkv, m4v, mov, ts, avi i inne),
przede wszystkim polską ścieżkę z audiodeskrypcją. Działa z pojedynczym filmem i z całym folderem (np. serialem).

Aktualna wersja: 1.1. Program jest w pełni obsługiwany klawiaturą i czytelny dla NVDA.
Można mu przekazać ścieżkę pliku lub folderu jako argument, np. przez skrót w menu „Wyślij do”.

## Pobieranie

Do wyboru są trzy warianty. Stałe linki zawsze prowadzą do najnowszej wersji:

- Instalator (zalecany): instaluje program, tworzy skrót w menu Start i opcjonalnie w „Wyślij do”,
  sam doinstalowuje brakujące .NET 10 i ffmpeg.
  https://github.com/Matius1024/WyciagaczAudio/releases/latest/download/WyciagaczAudio-instalator.exe
- Wersja portable: archiwum zip do rozpakowania w dowolnym miejscu, także na pendrive. Ma w sobie .NET,
  niczego nie instaluje w systemie, a ffmpeg pobiera przy pierwszym uruchomieniu do swojego folderu.
  https://github.com/Matius1024/WyciagaczAudio/releases/latest/download/WyciagaczAudio-portable.zip
- Sam program: jeden mały plik exe dla osób, które mają już .NET 10 Desktop Runtime
  (ffmpeg program też potrafi pobrać sam).
  https://github.com/Matius1024/WyciagaczAudio/releases/latest/download/WyciagaczAudio.exe

Wszystkie wersje: https://github.com/Matius1024/WyciagaczAudio/releases

## Obsługa klawiaturą

- Alt+I – wybierz plik, Alt+F – wybierz folder, Alt+P – uwzględnij podfoldery
- Alt+E – pobierz ffmpeg (przycisk jest widoczny tylko wtedy, gdy ffmpeg brakuje)
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

## ffmpeg i automatyczne pobieranie

Program korzysta z ffmpeg (`ffmpeg.exe` i `ffprobe.exe`). Szuka ich w tej kolejności:

1. folder programu (tam, gdzie leży `WyciagaczAudio.exe`),
2. podfolder `ffmpeg` w folderze programu,
3. zapasowy folder użytkownika `%LOCALAPPDATA%\WyciagaczAudio\ffmpeg`,
4. katalogi ze zmiennej PATH (np. ffmpeg zainstalowany poleceniem `winget install Gyan.FFmpeg`).

Używany jest pierwszy folder, w którym są oba pliki.

Przy każdym uruchomieniu program sprawdza, czy ffmpeg i ffprobe działają. Jeśli nie, pyta:
„Do działania programu potrzebny jest ffmpeg … Pobrać go teraz z gyan.dev (ok. 115 MB) i zapisać w folderze programu?”

- Tak – otwiera się okno „Pobieranie ffmpeg” z paskiem postępu. Stan pobierania jest czytany przez NVDA
  co 10% (np. „Pobrano 30% (34 z 115 MB)”), fokus stoi na przycisku Anuluj (działa też Escape).
  Archiwum `ffmpeg-release-essentials.zip` jest pobierane z gyan.dev do folderu tymczasowego, jego suma SHA-256
  jest porównywana z sumą opublikowaną przez gyan.dev, a potem wypakowywane są tylko `ffmpeg.exe` i `ffprobe.exe`
  do folderu programu. Gdy do folderu programu nie da się zapisywać (np. w `Program Files`), pliki trafiają
  do `%LOCALAPPDATA%\WyciagaczAudio\ffmpeg` i komunikat o tym mówi. Archiwum tymczasowe jest zawsze usuwane.
- Nie – program działa dalej, ale bez ffmpeg nie analizuje ani nie wyciąga audio. Przy próbie wczytania pliku
  lub folderu albo wyciągania pytanie pojawi się ponownie. Pobieranie można uruchomić w każdej chwili
  przyciskiem „Pobierz ffmpeg” (Alt+E).

Przy błędzie lub anulowaniu częściowo pobrane pliki są usuwane, a komunikat podaje przyczynę i inne sposoby:
`winget install Gyan.FFmpeg` albo ręczne pobranie z https://www.gyan.dev/ffmpeg/builds/ i wrzucenie
`ffmpeg.exe` i `ffprobe.exe` do folderu programu.

Do testów: zmienna środowiskowa `WYCIAGACZ_TEST_BEZ_PATH=1` sprawia, że program szuka ffmpeg tylko w folderze
programu (i jego podfolderze `ffmpeg`), z pominięciem `%LOCALAPPDATA%` i PATH.

## Wersja portable

Plik `WyciagaczAudio-1.1-portable.zip` zawiera folder `Wyciagacz audio portable` (nazwa bez polskich znaków,
żeby każde narzędzie rozpakowało ją tak samo), a w nim:

- `WyciagaczAudio.exe` – program z wbudowanym .NET 10 (nie trzeba instalować .NET),
- `README.md` – ten opis,
- `Instrukcja.txt` – krótka instrukcja.

Folder można rozpakować gdziekolwiek, np. na pendrive. Przy pierwszym uruchomieniu program zaproponuje pobranie
ffmpeg do swojego folderu. Nic nie jest instalowane w systemie; żeby usunąć program, wystarczy skasować folder
(i ewentualnie `%LOCALAPPDATA%\WyciagaczAudio`, jeśli ffmpeg trafił tam, bo folder programu był tylko do odczytu).

## Instalator

Plik `WyciagaczAudio-1.1-instalator.exe` instaluje program razem z brakującymi komponentami.

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
  a sam program przy uruchomieniu zaproponuje pobranie ffmpeg.
- Odinstalowanie: Ustawienia, Aplikacje, Zainstalowane aplikacje, „Wyciągacz audio 1.1”, Odinstaluj
  (albo `unins000.exe` w folderze programu). Usuwa program, skróty, wpis „Wyślij do” oraz ffmpeg położony w folderze programu.
  .NET zostaje w systemie.
- Parametry do testów: `/WYMUSFFMPEG` traktuje ffmpeg jako brakujący, `/BEZNET` nigdy nie instaluje .NET.
  Działa też tryb cichy, np. `/VERYSILENT /SUPPRESSMSGBOXES /CURRENTUSER`.

## Wymagania

- Windows 10 lub 11, 64-bitowy (x64).
- .NET 10 Desktop Runtime – tylko dla samego pliku `WyciagaczAudio.exe` (instalator doinstaluje go sam,
  wersja portable ma go w sobie): https://dotnet.microsoft.com/download/dotnet/10.0 – na stronie wybierz
  „.NET Desktop Runtime”, wersję x64. Gdy go brakuje, samego exe nie da się uruchomić; Windows (.NET) pokazuje wtedy
  własne okno z propozycją pobrania .NET. Program nie może tego obsłużyć sam, bo bez .NET w ogóle nie startuje.
  Kto nie chce instalować .NET, niech wybierze wersję portable.
- ffmpeg i ffprobe – program pobiera je sam (patrz wyżej), można też użyć `winget install Gyan.FFmpeg`.

## Historia zmian

- 1.1
  - wersja portable z wbudowanym .NET (zip, bez instalacji),
  - sprawdzanie ffmpeg przy uruchomieniu i pytanie o pobranie go z gyan.dev do folderu programu
    (z kontrolą sumy SHA-256 i oknem postępu czytelnym dla NVDA),
  - przycisk „Pobierz ffmpeg” (Alt+E), widoczny, gdy ffmpeg brakuje,
  - ffmpeg szukany także w podfolderze `ffmpeg` i w `%LOCALAPPDATA%\WyciagaczAudio\ffmpeg`.
- 1.0
  - pierwsze wydanie: wykrywanie audiodeskrypcji, praca na plikach i całych folderach, odsłuch próbki,
    formaty bez konwersji, MP3, AAC, FLAC i WAV, instalator z doinstalowaniem .NET 10 i ffmpeg.

## Budowanie ze źródeł

Potrzebny jest .NET 10 SDK i (dla instalatora) Inno Setup 6.1 lub nowszy. W folderze projektu:

```
powershell -ExecutionPolicy Bypass -File .\zbuduj-wszystko.ps1
```

Skrypt odczytuje wersję z `WyciagaczAudio.csproj` (musi się zgadzać z `#define AppVer` w `instalator\WyciagaczAudio.iss`)
i tworzy w folderze `wydania`:

- `WyciagaczAudio-<wersja>.exe` – zwykły program (kopia trafia też do `WyciagaczAudio.exe` w folderze projektu),
- `WyciagaczAudio-<wersja>-portable.zip` – wersja portable,
- `WyciagaczAudio-<wersja>-instalator.exe` – instalator.

Pliki starszych wersji w `wydania` zostają. Poszczególne kroki ręcznie:

```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-portable
powershell -ExecutionPolicy Bypass -File .\zbuduj-instalator.ps1
```

`zbuduj-instalator.ps1` bierze program z `wydania\WyciagaczAudio-<wersja>.exe`.

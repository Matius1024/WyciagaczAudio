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

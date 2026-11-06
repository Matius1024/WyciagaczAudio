# Buduje instalator programu „Wyciągacz audio” za pomocą Inno Setup 6 (ISCC.exe).
# Użycie: powershell -ExecutionPolicy Bypass -File .\zbuduj-instalator.ps1
# Wynik: wydania\WyciagaczAudio-<wersja>-instalator.exe

$ErrorActionPreference = 'Stop'
$projekt = $PSScriptRoot
$skrypt = Join-Path $projekt 'instalator\WyciagaczAudio.iss'

$kandydaci = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$iscc = $kandydaci | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $iscc) {
    $polecenie = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($polecenie) { $iscc = $polecenie.Source }
}
if (-not $iscc) {
    Write-Host 'Błąd: nie znaleziono Inno Setup 6 (ISCC.exe). Zainstaluj Inno Setup 6.1 lub nowszy.'
    exit 1
}

$wersja = ([regex]::Match((Get-Content -LiteralPath $skrypt -Encoding UTF8 -Raw), '#define AppVer "([^"]+)"')).Groups[1].Value
$exe = Join-Path $projekt "wydania\WyciagaczAudio-$wersja.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "Błąd: brak pliku programu $exe. Najpierw zbuduj program (dotnet publish) i skopiuj exe do folderu wydania."
    exit 1
}

Write-Host "Kompilator: $iscc"
Write-Host "Budowanie instalatora wersji $wersja..."
& $iscc $skrypt
if ($LASTEXITCODE -ne 0) {
    Write-Host "Błąd: kompilacja instalatora nie powiodła się (kod $LASTEXITCODE)."
    exit $LASTEXITCODE
}

$wynik = Join-Path $projekt "wydania\WyciagaczAudio-$wersja-instalator.exe"
$rozmiar = [math]::Round((Get-Item -LiteralPath $wynik).Length / 1MB, 1)
Write-Host "Gotowe: $wynik ($rozmiar MB)"

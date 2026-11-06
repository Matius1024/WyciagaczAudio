# Buduje wszystkie pliki wydania programu „Wyciągacz audio” dla wersji z WyciagaczAudio.csproj:
#  - zwykły program (wymaga .NET 10 Desktop Runtime): wydania\WyciagaczAudio-<wersja>.exe
#    (kopia trafia też do WyciagaczAudio.exe w głównym folderze projektu),
#  - wersję portable z wbudowanym .NET: wydania\WyciagaczAudio-<wersja>-portable.zip,
#  - instalator: wydania\WyciagaczAudio-<wersja>-instalator.exe (przez zbuduj-instalator.ps1).
# Pliki poprzednich wersji w folderze wydania zostają bez zmian.
# Użycie: powershell -ExecutionPolicy Bypass -File .\zbuduj-wszystko.ps1

$ErrorActionPreference = 'Stop'
$projekt = $PSScriptRoot
Set-Location -LiteralPath $projekt

# Wersja z csproj, np. 1.1.0 -> 1.1 (końcowe .0 jest pomijane)
$csproj = Get-Content -LiteralPath (Join-Path $projekt 'WyciagaczAudio.csproj') -Encoding UTF8 -Raw
$pelna = ([regex]::Match($csproj, '<Version>([^<]+)</Version>')).Groups[1].Value
if (-not $pelna) { Write-Host 'Błąd: brak <Version> w WyciagaczAudio.csproj.'; exit 1 }
$wersja = if ($pelna -match '^\d+\.\d+\.0$') { $pelna -replace '\.0$', '' } else { $pelna }
Write-Host "Wersja: $wersja"

$iss = Get-Content -LiteralPath (Join-Path $projekt 'instalator\WyciagaczAudio.iss') -Encoding UTF8 -Raw
$wersjaIss = ([regex]::Match($iss, '#define AppVer "([^"]+)"')).Groups[1].Value
if ($wersjaIss -ne $wersja) {
    Write-Host "Błąd: wersja w instalator\WyciagaczAudio.iss ($wersjaIss) różni się od wersji w csproj ($wersja)."
    exit 1
}

$wydania = Join-Path $projekt 'wydania'
New-Item -ItemType Directory -Force -Path $wydania | Out-Null

function Uruchom([string]$opis, [string[]]$argumenty) {
    Write-Host $opis
    & dotnet @argumenty
    if ($LASTEXITCODE -ne 0) { Write-Host "Błąd: $opis – kod $LASTEXITCODE."; exit $LASTEXITCODE }
}

# 1. Zwykły program (framework-dependent, jeden plik)
Uruchom 'Budowanie zwykłego programu...' @('publish', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false',
    '-p:PublishSingleFile=true', '-o', 'publish')
$exe = Join-Path $wydania "WyciagaczAudio-$wersja.exe"
Copy-Item -LiteralPath (Join-Path $projekt 'publish\WyciagaczAudio.exe') -Destination $exe -Force
Copy-Item -LiteralPath (Join-Path $projekt 'publish\WyciagaczAudio.exe') -Destination (Join-Path $projekt 'WyciagaczAudio.exe') -Force

# 2. Wersja portable (self-contained, jeden plik z wbudowanym .NET)
Uruchom 'Budowanie wersji portable...' @('publish', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true', '-p:EnableCompressionInSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-o', 'publish-portable')

# Archiwum zip budowane ręcznie przez ZipArchive: separatory „/” i nazwa folderu bez polskich znaków,
# żeby każde narzędzie (Eksplorator, 7-Zip, tar) rozpakowało je tak samo.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$folderWZipie = 'Wyciagacz audio portable'
$zip = Join-Path $wydania "WyciagaczAudio-$wersja-portable.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
$pliki = @(
    @{ Zrodlo = (Join-Path $projekt 'publish-portable\WyciagaczAudio.exe'); Nazwa = 'WyciagaczAudio.exe' },
    @{ Zrodlo = (Join-Path $projekt 'README.md'); Nazwa = 'README.md' },
    @{ Zrodlo = (Join-Path $projekt 'portable\Instrukcja.txt'); Nazwa = 'Instrukcja.txt' }
)
$archiwum = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($p in $pliki) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archiwum, $p.Zrodlo, "$folderWZipie/$($p.Nazwa)",
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $archiwum.Dispose()
}

# 3. Instalator
Write-Host 'Budowanie instalatora...'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projekt 'zbuduj-instalator.ps1')
if ($LASTEXITCODE -ne 0) { Write-Host "Błąd: budowanie instalatora – kod $LASTEXITCODE."; exit $LASTEXITCODE }

Write-Host ''
Write-Host 'Gotowe pliki:'
foreach ($n in @("WyciagaczAudio-$wersja.exe", "WyciagaczAudio-$wersja-portable.zip", "WyciagaczAudio-$wersja-instalator.exe")) {
    $f = Get-Item -LiteralPath (Join-Path $wydania $n)
    Write-Host ("  {0} ({1:N1} MB, {2} bajtów)" -f $f.FullName, ($f.Length / 1MB), $f.Length)
}

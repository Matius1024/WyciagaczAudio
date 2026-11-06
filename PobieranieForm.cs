using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows.Forms.Automation;

namespace WyciagaczAudio;

/// <summary>
/// Modalne okno pobierania ffmpeg z gyan.dev: pobiera archiwum do %TEMP%, sprawdza sumę SHA-256
/// (jeśli serwer ją udostępnia), wypakowuje tylko ffmpeg.exe i ffprobe.exe do folderu programu
/// (albo do %LOCALAPPDATA%\WyciagaczAudio\ffmpeg, gdy do folderu programu nie da się pisać).
/// Wynik czyta okno główne z właściwości po zamknięciu.
/// </summary>
public sealed class PobieranieForm : Form
{
    public const string Url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    const string UserAgent = "WyciagaczAudio/1.1";

    readonly Label lblOpis = new() { AutoSize = true };
    readonly Label lblStan = new() { AutoSize = true, Text = "Łączenie z gyan.dev..." };
    readonly ProgressBar pasek = new() { Dock = DockStyle.Fill, Maximum = 100, AccessibleName = "Postęp pobierania" };
    readonly Button btnAnuluj = new() { Text = "Anuluj", AutoSize = true };

    readonly CancellationTokenSource cts = new();
    bool trwa;

    public bool Sukces { get; private set; }
    public bool Anulowano { get; private set; }
    public string Blad { get; private set; } = "";
    public string Folder { get; private set; } = "";
    public bool UzytoZapasowego { get; private set; }
    /// <summary>null – serwer nie udostępnił sumy, true – suma zgodna.</summary>
    public bool? SumaSprawdzona { get; private set; }

    public PobieranieForm(string? tytulRodzica)
    {
        Text = "Pobieranie ffmpeg" + (tytulRodzica != null ? " – " + tytulRodzica : "");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Width = 560;
        Height = 220;

        (Folder, UzytoZapasowego) = WybierzFolder();
        lblOpis.Text = $"Pobieranie ffmpeg z gyan.dev (ok. 115 MB) do folderu:\n{Folder}";
        lblOpis.MaximumSize = new Size(520, 0);
        lblStan.LiveSetting = AutomationLiveSetting.Polite;
        lblStan.MaximumSize = new Size(520, 0);
        btnAnuluj.AccessibleDescription = "Przerywa pobieranie ffmpeg. Klawisz Escape.";

        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lblOpis.TabIndex = 0; lblStan.TabIndex = 1; pasek.TabIndex = 2; btnAnuluj.TabIndex = 3;
        t.Controls.Add(lblOpis, 0, 0);
        t.Controls.Add(lblStan, 0, 1);
        t.Controls.Add(pasek, 0, 2);
        t.Controls.Add(btnAnuluj, 0, 3);
        Controls.Add(t);

        CancelButton = btnAnuluj;
        btnAnuluj.Click += (_, _) =>
        {
            if (!trwa) { if (!Sukces) Anulowano = true; Close(); return; }
            btnAnuluj.Enabled = false;
            Stan("Anulowanie...");
            cts.Cancel();
        };
        FormClosing += (_, e) =>
        {
            // zamknięcie okna w trakcie pobierania = anulowanie; okno zamknie się samo po sprzątnięciu
            if (trwa) { e.Cancel = true; cts.Cancel(); }
            else if (!Sukces && Blad.Length == 0) Anulowano = true;
        };
        Shown += async (_, _) =>
        {
            btnAnuluj.Focus();
            await Task.Yield();
            // okno mogło zostać zamknięte (Escape), zanim pobieranie wystartowało
            if (IsDisposed || !Visible || Anulowano) return;
            await PobierzAsync();
            Close();
        };
    }

    void Stan(string s) => lblStan.Text = s;

    /// <summary>Folder programu, jeśli da się do niego pisać; w przeciwnym razie folder zapasowy.</summary>
    static (string folder, bool zapasowy) WybierzFolder()
    {
        var app = Ffmpeg.FolderProgramu;
        try
        {
            var test = Path.Combine(app, $".test-zapisu-{Guid.NewGuid():N}");
            File.WriteAllText(test, "");
            File.Delete(test);
            return (app, false);
        }
        catch
        {
            return (Ffmpeg.FolderZapasowy, true);
        }
    }

    static string Mb(long b) => (b / 1_000_000.0).ToString("0"); // MB dziesiętne, jak w „ok. 115 MB”

    async Task PobierzAsync()
    {
        trwa = true;
        var ct = cts.Token;
        var zip = Path.Combine(Path.GetTempPath(), $"WyciagaczAudio-ffmpeg-{Environment.ProcessId}.zip");
        var czesciowe = new List<string>();
        try
        {
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            // suma kontrolna – jeśli gyan.dev jej nie poda, pobieramy bez sprawdzania
            string? sumaOczekiwana = null;
            try
            {
                using var c = CancellationTokenSource.CreateLinkedTokenSource(ct);
                c.CancelAfter(TimeSpan.FromSeconds(30));
                var txt = (await http.GetStringAsync(Url + ".sha256", c.Token)).Trim();
                var pierwsze = txt.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                if (pierwsze.Length == 64 && pierwsze.All(Uri.IsHexDigit)) sumaOczekiwana = pierwsze.ToLowerInvariant();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { }

            using (var naglowki = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                naglowki.CancelAfter(TimeSpan.FromSeconds(60));
                using var resp = await http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, naglowki.Token);
                if (!resp.IsSuccessStatusCode)
                    throw new BladPobierania($"serwer odpowiedział kodem {(int)resp.StatusCode} ({resp.ReasonPhrase})");
                long? razem = resp.Content.Headers.ContentLength;
                Stan(razem is > 0 ? $"Pobieranie rozpoczęte, rozmiar {Mb(razem.Value)} MB." : "Pobieranie rozpoczęte.");

                czesciowe.Add(zip);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (var wej = await resp.Content.ReadAsStreamAsync(ct))
                await using (var wyj = new FileStream(zip, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true))
                {
                    var buf = new byte[1 << 16];
                    long pobrano = 0;
                    int ostatniProg = 0;
                    long ostatnieMb = 0;
                    while (true)
                    {
                        // brak danych przez 60 s = zerwane połączenie
                        using var odczyt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        odczyt.CancelAfter(TimeSpan.FromSeconds(60));
                        int n;
                        try { n = await wej.ReadAsync(buf, odczyt.Token); }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                        {
                            throw new BladPobierania("serwer przestał wysyłać dane (przekroczony czas oczekiwania)");
                        }
                        if (n == 0) break;
                        await wyj.WriteAsync(buf.AsMemory(0, n), ct);
                        hash.AppendData(buf, 0, n);
                        pobrano += n;
                        if (razem is > 0)
                        {
                            int proc = (int)Math.Min(100, pobrano * 100 / razem.Value);
                            pasek.Value = proc;
                            // komunikat tylko co 10%, żeby NVDA nie mówił bez przerwy
                            if (proc / 10 > ostatniProg / 10)
                            {
                                ostatniProg = proc;
                                Stan($"Pobrano {proc / 10 * 10}% ({Mb(pobrano)} z {Mb(razem.Value)} MB)");
                            }
                        }
                        else if (pobrano - ostatnieMb >= 10_000_000)
                        {
                            ostatnieMb = pobrano;
                            Stan($"Pobrano {Mb(pobrano)} MB");
                        }
                    }
                    if (razem is > 0 && pobrano != razem.Value)
                        throw new BladPobierania($"pobrano tylko {Mb(pobrano)} z {Mb(razem.Value)} MB (połączenie zostało przerwane)");
                }

                var suma = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                if (sumaOczekiwana != null)
                {
                    if (suma != sumaOczekiwana)
                        throw new BladPobierania("suma kontrolna SHA-256 pobranego pliku się nie zgadza (plik uszkodzony lub podmieniony)");
                    SumaSprawdzona = true;
                }
            }

            ct.ThrowIfCancellationRequested();
            Stan(SumaSprawdzona == true ? "Suma kontrolna zgodna. Rozpakowywanie ffmpeg..." : "Rozpakowywanie ffmpeg...");
            btnAnuluj.Enabled = false; // rozpakowanie trwa kilka sekund, nie przerywamy go w połowie
            await Task.Run(() => Rozpakuj(zip, Folder, czesciowe));
            Sukces = true;
            Stan("Gotowe.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Anulowano = true;
        }
        catch (BladPobierania e) { Blad = e.Message; }
        catch (HttpRequestException e) { Blad = "błąd połączenia z gyan.dev: " + e.Message; }
        catch (UnauthorizedAccessException e) { Blad = "brak uprawnień do zapisu: " + e.Message; }
        catch (IOException e) { Blad = "błąd zapisu pliku: " + e.Message; }
        catch (InvalidDataException e) { Blad = "pobrane archiwum jest uszkodzone: " + e.Message; }
        catch (Exception e) { Blad = e.Message; }
        finally
        {
            TryDelete(zip);
            if (!Sukces) foreach (var f in czesciowe) TryDelete(f);
            trwa = false;
        }
    }

    static void Rozpakuj(string zip, string folder, List<string> czesciowe)
    {
        Directory.CreateDirectory(folder);
        using var arch = ZipFile.OpenRead(zip);
        var nazwy = new[] { "ffmpeg.exe", "ffprobe.exe" };
        var wpisy = new List<(ZipArchiveEntry wpis, string cel, string tymczasowy)>();
        foreach (var nazwa in nazwy)
        {
            var wpis = arch.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').EndsWith("/bin/" + nazwa, StringComparison.OrdinalIgnoreCase))
                       ?? throw new BladPobierania($"w pobranym archiwum nie ma pliku bin/{nazwa}");
            var cel = Path.Combine(folder, nazwa);
            wpisy.Add((wpis, cel, cel + ".czesciowy"));
        }
        foreach (var (wpis, _, tymczasowy) in wpisy)
        {
            czesciowe.Add(tymczasowy);
            wpis.ExtractToFile(tymczasowy, true);
        }
        foreach (var (_, cel, tymczasowy) in wpisy)
        {
            File.Move(tymczasowy, cel, true);
            czesciowe.Remove(tymczasowy);
        }
    }

    static void TryDelete(string f)
    {
        try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) cts.Dispose();
        base.Dispose(disposing);
    }

    sealed class BladPobierania(string msg) : Exception(msg);
}

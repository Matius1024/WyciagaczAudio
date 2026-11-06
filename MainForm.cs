using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms.Automation;

namespace WyciagaczAudio;

public sealed class MainForm : Form
{
    const string Wersja = "1.0";

    readonly Label lblSource = new() { Text = "Źródło (plik lub folder):", AutoSize = true };
    readonly TextBox txtSource = new() { ReadOnly = true, Dock = DockStyle.Fill, AccessibleName = "Źródło (plik lub folder)" };
    readonly Button btnFile = new() { Text = "Wybierz pl&ik...", AutoSize = true };
    readonly Button btnFolder = new() { Text = "Wybierz &folder...", AutoSize = true };
    readonly CheckBox chkRecursive = new() { Text = "Uwzględnij &podfoldery", AutoSize = true, Checked = true };
    readonly Label lblTracks = new() { Text = "Ścieżki &audio (spacja zaznacza lub odznacza):", AutoSize = true };
    readonly CheckedListBox lstTracks = new() { CheckOnClick = true, Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "Ścieżki audio" };
    readonly Button btnPreview = new() { Text = "&Odsłuchaj próbkę podświetlonej ścieżki", AutoSize = true };
    readonly Label lblFormat = new() { Text = "Fo&rmat wyjściowy:", AutoSize = true, Anchor = AnchorStyles.Left };
    readonly ComboBox cmbFormat = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420, AccessibleName = "Format wyjściowy" };
    readonly Label lblQuality = new() { Text = "&Jakość (formaty stratne):", AutoSize = true, Anchor = AnchorStyles.Left };
    readonly ComboBox cmbQuality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420, AccessibleName = "Jakość (formaty stratne)", Enabled = false };
    readonly CheckBox chkStereo = new() { Text = "Zmiksuj do &stereo", AutoSize = true };
    readonly Label lblOut = new() { Text = "Folder &docelowy:", AutoSize = true };
    readonly TextBox txtOut = new() { Dock = DockStyle.Fill, AccessibleName = "Folder docelowy" };
    readonly Button btnOut = new() { Text = "Wy&bierz folder docelowy...", AutoSize = true };
    readonly CheckBox chkOverwrite = new() { Text = "&Nadpisuj istniejące pliki", AutoSize = true };
    readonly Button btnStart = new() { Text = "&Wyciągnij audio", AutoSize = true };
    readonly Button btnCancel = new() { Text = "Anu&luj", AutoSize = true, Enabled = false };
    readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Maximum = 1000, AccessibleName = "Postęp" };
    readonly Label lblStatus = new() { AutoSize = true, Text = "Wybierz plik albo folder z filmami." };
    readonly Label lblLog = new() { Text = "Dzienni&k:", AutoSize = true };
    readonly TextBox txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AccessibleName = "Dziennik" };

    readonly ToolTip tips = new();

    string? sourcePath;
    bool sourceIsFile;
    List<MediaInfo> infos = new();
    List<TrackGroup> groups = new();
    bool outEditedByUser;
    bool settingOut;
    CancellationTokenSource? cts;
    bool busy;

    public MainForm(string? startPath)
    {
        Text = $"Wyciągacz audio {Wersja}";
        Width = 900;
        Height = 760;
        MinimumSize = new Size(600, 500);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        cmbFormat.Items.AddRange(new object[]
        {
            "Bez konwersji – oryginalna jakość (zalecane)",
            "MP3 (zawsze stereo)",
            "AAC w pliku M4A",
            "FLAC – bezstratny",
            "WAV – bez kompresji",
        });
        cmbFormat.SelectedIndex = 0;
        foreach (var k in Ffmpeg.Przeplywnosci)
            cmbQuality.Items.Add(k switch
            {
                64 => "64 kb/s – niska, mały plik",
                128 => "128 kb/s – średnia",
                192 => "192 kb/s – dobra (zalecana)",
                256 => "256 kb/s – bardzo dobra",
                320 => "320 kb/s – najwyższa",
                _ => $"{k} kb/s",
            });
        cmbQuality.SelectedIndex = Array.IndexOf(Ffmpeg.Przeplywnosci, 192);
        cmbQuality.AccessibleDescription = "Przy AAC z dźwiękiem wielokanałowym (5.1) przepływność jest podwajana.";
        chkStereo.Enabled = false;
        lblStatus.LiveSetting = AutomationLiveSetting.Polite;
        lstTracks.AccessibleDescription = "Zaznacz ścieżki do wyciągnięcia. Audiodeskrypcja jest zaznaczana automatycznie, jeśli zostanie wykryta.";
        tips.SetToolTip(chkStereo, "Przy formacie bez konwersji nie da się zmienić liczby kanałów.");

        BuildLayout();

        btnFile.Click += (_, _) => PickFile();
        btnFolder.Click += (_, _) => PickFolder();
        chkRecursive.CheckedChanged += async (_, _) => { if (sourcePath != null && !sourceIsFile) await LoadAsync(sourcePath, false); };
        lstTracks.ItemCheck += (_, _) => BeginInvoke(() => { if (!outEditedByUser) FillDefaultOut(); });
        btnPreview.Click += async (_, _) => await PreviewAsync();
        cmbFormat.SelectedIndexChanged += (_, _) =>
        {
            chkStereo.Enabled = cmbFormat.SelectedIndex != 0 && cmbFormat.SelectedIndex != 1;
            if (!chkStereo.Enabled) chkStereo.Checked = false;
            cmbQuality.Enabled = IsLossy;
        };
        txtOut.TextChanged += (_, _) => { if (!settingOut) outEditedByUser = true; };
        btnOut.Click += (_, _) => PickOut();
        btnStart.Click += async (_, _) => await ExtractAsync();
        btnCancel.Click += (_, _) => { cts?.Cancel(); Status("Anulowanie..."); };
        AcceptButton = btnStart;
        CancelButton = btnCancel;

        FormClosing += (_, e) =>
        {
            if (busy && MessageBox.Show(this, "Trwa praca. Przerwać i zamknąć program?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                e.Cancel = true;
            else cts?.Cancel();
        };

        Shown += async (_, _) =>
        {
            if (!Ffmpeg.Dostepny(out var err))
            {
                MessageBox.Show(this,
                    "Nie znaleziono programu ffmpeg lub ffprobe.\n\nZainstaluj go poleceniem w PowerShell:\nwinget install Gyan.FFmpeg\n\nalbo skopiuj ffmpeg.exe i ffprobe.exe do folderu tego programu.\n\nSzczegóły: " + err,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            CleanPreviews();
            if (!string.IsNullOrWhiteSpace(startPath))
            {
                var p = startPath.Trim('"');
                if (File.Exists(p)) await LoadAsync(p, true);
                else if (Directory.Exists(p)) await LoadAsync(p, false);
            }
            else btnFolder.Focus();
        };
    }

    void BuildLayout()
    {
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        FlowLayoutPanel Flow(params Control[] cs)
        {
            var f = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
            int i = 0;
            foreach (var c in cs) { c.TabIndex = i++; f.Controls.Add(c); }
            return f;
        }

        var outRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0) };
        outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        txtOut.TabIndex = 0; btnOut.TabIndex = 1;
        outRow.Controls.Add(txtOut, 0, 0);
        outRow.Controls.Add(btnOut, 1, 0);

        var rows = new (Control c, SizeType t, float h)[]
        {
            (lblSource, SizeType.AutoSize, 0),
            (txtSource, SizeType.AutoSize, 0),
            (Flow(btnFile, btnFolder, chkRecursive), SizeType.AutoSize, 0),
            (lblTracks, SizeType.AutoSize, 0),
            (lstTracks, SizeType.Percent, 50),
            (Flow(btnPreview), SizeType.AutoSize, 0),
            (Flow(lblFormat, cmbFormat, chkStereo), SizeType.AutoSize, 0),
            (Flow(lblQuality, cmbQuality), SizeType.AutoSize, 0),
            (lblOut, SizeType.AutoSize, 0),
            (outRow, SizeType.AutoSize, 0),
            (chkOverwrite, SizeType.AutoSize, 0),
            (Flow(btnStart, btnCancel), SizeType.AutoSize, 0),
            (progress, SizeType.Absolute, 24),
            (lblStatus, SizeType.AutoSize, 0),
            (lblLog, SizeType.AutoSize, 0),
            (txtLog, SizeType.Percent, 50),
        };
        main.RowCount = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            main.RowStyles.Add(rows[i].t == SizeType.AutoSize ? new RowStyle(SizeType.AutoSize) : new RowStyle(rows[i].t, rows[i].h));
            rows[i].c.TabIndex = i;
            main.Controls.Add(rows[i].c, 0, i);
        }
        lblStatus.MaximumSize = new Size(860, 0);
        Controls.Add(main);
    }

    // ---------- pomocnicze ----------

    void Status(string s) => lblStatus.Text = s;

    void Log(string s)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {s}";
        if (txtLog.TextLength > 0) txtLog.AppendText(Environment.NewLine);
        txtLog.AppendText(line);
    }

    void SetBusy(bool b)
    {
        busy = b;
        btnFile.Enabled = btnFolder.Enabled = chkRecursive.Enabled = !b;
        lstTracks.Enabled = btnPreview.Enabled = cmbFormat.Enabled = !b;
        txtOut.Enabled = btnOut.Enabled = chkOverwrite.Enabled = btnStart.Enabled = !b;
        chkStereo.Enabled = !b && cmbFormat.SelectedIndex > 1;
        cmbQuality.Enabled = !b && IsLossy;
        btnCancel.Enabled = b;
        UseWaitCursor = b;
    }

    bool IsLossy => cmbFormat.SelectedIndex is (int)OutFormat.Mp3 or (int)OutFormat.Aac;

    string RootFolder => sourceIsFile ? Path.GetDirectoryName(sourcePath!)! : sourcePath!;

    List<TrackGroup> CheckedGroups() => lstTracks.CheckedIndices.Cast<int>().Select(i => groups[i]).ToList();

    void FillDefaultOut()
    {
        if (sourcePath == null) return;
        var sel = CheckedGroups();
        string suffix;
        if (sel.Any(g => g.IsAD)) suffix = "audiodeskrypcja";
        else if (sel.Count > 0 && sel.Select(g => Opisy.Jezyk(g.Language)).Distinct().Count() == 1)
            suffix = "audio " + Opisy.Jezyk(sel[0].Language);
        else suffix = "audio";

        var root = RootFolder.TrimEnd(Path.DirectorySeparatorChar);
        var name = Path.GetFileName(root);
        var parent = Path.GetDirectoryName(root);
        string outDir = string.IsNullOrEmpty(name) || parent == null
            ? Path.Combine(RootFolder, suffix)                  // np. cały dysk D:\
            : Path.Combine(parent, $"{name} {suffix}");
        settingOut = true;
        txtOut.Text = outDir;
        settingOut = false;
        outEditedByUser = false;
    }

    // ---------- wybór źródła ----------

    async void PickFile()
    {
        using var d = new OpenFileDialog
        {
            Title = "Wybierz plik wideo",
            Filter = "Pliki wideo|" + string.Join(";", Ffmpeg.VideoExt.Select(e => "*" + e)) + "|Wszystkie pliki|*.*",
        };
        if (d.ShowDialog(this) == DialogResult.OK) await LoadAsync(d.FileName, true);
    }

    async void PickFolder()
    {
        using var d = new FolderBrowserDialog { Description = "Wybierz folder z filmami lub serialem", UseDescriptionForTitle = true };
        if (sourcePath != null) d.InitialDirectory = RootFolder;
        if (d.ShowDialog(this) == DialogResult.OK) await LoadAsync(d.SelectedPath, false);
    }

    void PickOut()
    {
        using var d = new FolderBrowserDialog { Description = "Wybierz folder docelowy", UseDescriptionForTitle = true };
        if (txtOut.Text.Length > 0)
        {
            var existing = txtOut.Text;
            while (!Directory.Exists(existing) && Path.GetDirectoryName(existing) is { } up) existing = up;
            if (Directory.Exists(existing)) d.InitialDirectory = existing;
        }
        if (d.ShowDialog(this) == DialogResult.OK)
        {
            txtOut.Text = d.SelectedPath;
            outEditedByUser = true;
        }
    }

    async Task LoadAsync(string path, bool isFile)
    {
        sourcePath = path;
        sourceIsFile = isFile;
        txtSource.Text = path;
        lstTracks.Items.Clear();
        groups.Clear();
        infos.Clear();
        progress.Value = 0;

        List<string> files;
        if (isFile) files = new() { path };
        else
        {
            var opt = chkRecursive.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            try
            {
                files = Directory.EnumerateFiles(path, "*", new EnumerationOptions
                    {
                        RecurseSubdirectories = opt == SearchOption.AllDirectories,
                        IgnoreInaccessible = true,
                    })
                    .Where(f => Ffmpeg.VideoExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();
            }
            catch (Exception e)
            {
                Status("Nie udało się odczytać folderu: " + e.Message);
                return;
            }
            files.Sort((a, b) => Opisy.PorownajNaturalnie(a, b));
        }

        if (files.Count == 0)
        {
            Status("W wybranym folderze nie ma plików wideo.");
            Log($"Brak plików wideo w: {path}");
            return;
        }

        SetBusy(true);
        cts = new CancellationTokenSource();
        var ct = cts.Token;
        try
        {
            Status(files.Count == 1 ? "Analizuję plik..." : $"Analizuję {Opisy.Pliki(files.Count)}...");
            var results = new MediaInfo[files.Count];
            int done = 0;
            await Parallel.ForEachAsync(Enumerable.Range(0, files.Count),
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                async (i, token) =>
                {
                    results[i] = await Ffmpeg.ProbeAsync(files[i], token);
                    var n = Interlocked.Increment(ref done);
                    BeginInvoke(() => progress.Value = n * 1000 / files.Count);
                });

            foreach (var r in results)
            {
                if (r.Error != null) Log($"Błąd analizy „{Path.GetFileName(r.Path)}”: {r.Error}");
                else if (r.Tracks.Count == 0) Log($"Brak ścieżek audio w „{Path.GetFileName(r.Path)}”.");
            }
            infos = results.Where(r => r.Error == null && r.Tracks.Count > 0).ToList();
            groups = Ffmpeg.Grupuj(infos);

            // audiodeskrypcja i polski na górę listy, reszta w kolejności z pliku
            groups = groups
                .OrderByDescending(g => g.IsAD && Opisy.JestPolski(g.Language))
                .ThenByDescending(g => Opisy.JestPolski(g.Language))
                .ThenByDescending(g => g.IsAD)
                .ToList();

            foreach (var g in groups) lstTracks.Items.Add(g.Describe(infos.Count));

            string msg;
            int adIdx = groups.FindIndex(g => g.IsAD && Opisy.JestPolski(g.Language));
            if (adIdx < 0) adIdx = groups.FindIndex(g => g.IsAD);
            if (adIdx >= 0)
            {
                lstTracks.SetItemChecked(adIdx, true);
                msg = $"Wykryto audiodeskrypcję ({Opisy.Jezyk(groups[adIdx].Language)}) i zaznaczono ją.";
            }
            else
            {
                int pl = groups.FindIndex(g => Opisy.JestPolski(g.Language));
                int plCount = groups.Count(g => Opisy.JestPolski(g.Language));
                if (pl >= 0)
                {
                    lstTracks.SetItemChecked(pl, true);
                    msg = plCount > 1
                        ? $"Nie wykryto oznaczonej audiodeskrypcji. Polskie ścieżki: {plCount} – zaznaczono pierwszą. Audiodeskrypcja to często ta druga; odsłuchaj próbkę, żeby sprawdzić."
                        : "Nie wykryto oznaczonej audiodeskrypcji. Zaznaczono ścieżkę polską.";
                }
                else msg = groups.Count > 0 ? "Nie wykryto audiodeskrypcji ani polskiej ścieżki. Zaznacz ścieżki ręcznie." : "Brak ścieżek audio.";
            }

            FillDefaultOut();
            int failed = files.Count - infos.Count;
            var head = Opisy.Pliki(infos.Count);
            Status($"Przeanalizowano {head}, znaleziono {Opisy.Ile(groups.Count, "rodzaj", "rodzaje", "rodzajów")} ścieżek audio" +
                   (failed > 0 ? $", z problemem: {Opisy.Pliki(failed)} (szczegóły w dzienniku)" : "") + ". " + msg);
            Log($"Wczytano: {path} ({head}).");
            progress.Value = 0;
        }
        catch (OperationCanceledException)
        {
            Status("Analiza przerwana.");
        }
        finally
        {
            cts?.Dispose();
            cts = null;
            SetBusy(false);
        }
        if (lstTracks.Items.Count > 0)
        {
            lstTracks.Focus();
            lstTracks.SelectedIndex = Math.Max(0, lstTracks.CheckedIndices.Count > 0 ? lstTracks.CheckedIndices[0] : 0);
        }
    }

    // ---------- odsłuch ----------

    static string PreviewDir => Path.Combine(Path.GetTempPath(), "WyciagaczAudio");

    static void CleanPreviews()
    {
        try
        {
            if (Directory.Exists(PreviewDir))
                foreach (var f in Directory.GetFiles(PreviewDir)) { try { File.Delete(f); } catch { } }
        }
        catch { }
    }

    async Task PreviewAsync()
    {
        // najpierw oddaj sterowanie pętli okna – inaczej przy aktywacji z zewnątrz (NVDA, UI Automation)
        // okno komunikatu potrafi się od razu zamknąć
        await Task.Yield();
        int idx = lstTracks.SelectedIndex;
        if (idx < 0 || idx >= groups.Count)
        {
            Status("Najpierw przejdź strzałkami na ścieżkę na liście.");
            return;
        }
        var g = groups[idx];
        var info = infos.First(i => i.ByKey.ContainsKey(g.Key));
        var track = info.ByKey[g.Key];
        double start = info.Duration > 120 ? Math.Round(info.Duration * 0.2) : 0;

        Directory.CreateDirectory(PreviewDir);
        var outFile = Path.Combine(PreviewDir, $"probka-{DateTime.Now:HHmmss}.m4a");
        SetBusy(true);
        cts = new CancellationTokenSource();
        try
        {
            Status($"Przygotowuję minutową próbkę ścieżki: {g.ShortName()}...");
            var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-loglevel", "error",
                "-ss", start.ToString(CultureInfo.InvariantCulture), "-i", info.Path, "-t", "60",
                "-map", $"0:{track.Index}", "-vn", "-sn", "-dn", "-c:a", "aac", "-b:a", "160k", "-ac", "2", outFile };
            var (code, errors) = await Ffmpeg.RunAsync(args, null, cts.Token);
            if (code != 0 || !File.Exists(outFile))
            {
                Status("Nie udało się przygotować próbki. Szczegóły w dzienniku.");
                Log("Błąd próbki: " + errors);
                return;
            }
            Status($"Odtwarzam próbkę z pliku „{Path.GetFileName(info.Path)}” od {TimeSpan.FromSeconds(start):h\\:mm\\:ss}.");
            Process.Start(new ProcessStartInfo(outFile) { UseShellExecute = true });
        }
        catch (OperationCanceledException) { Status("Przerwano przygotowanie próbki."); }
        catch (Exception e) { Status("Nie udało się odtworzyć próbki: " + e.Message); }
        finally
        {
            cts?.Dispose();
            cts = null;
            SetBusy(false);
        }
    }

    // ---------- wyciąganie ----------

    async Task ExtractAsync()
    {
        // najpierw oddaj sterowanie pętli okna – inaczej przy aktywacji z zewnątrz (NVDA, UI Automation)
        // okno komunikatu potrafi się od razu zamknąć
        await Task.Yield();
        if (sourcePath == null || infos.Count == 0)
        {
            Status("Najpierw wybierz plik albo folder z filmami.");
            btnFolder.Focus();
            return;
        }
        var sel = CheckedGroups();
        if (sel.Count == 0)
        {
            Status("Nie zaznaczono żadnej ścieżki audio.");
            lstTracks.Focus();
            return;
        }
        var outRoot = txtOut.Text.Trim();
        if (outRoot.Length == 0 || !Path.IsPathFullyQualified(outRoot))
        {
            Status("Podaj pełną ścieżkę folderu docelowego.");
            txtOut.Focus();
            return;
        }

        var format = (OutFormat)cmbFormat.SelectedIndex;
        bool stereo = chkStereo.Checked;
        int kbps = Ffmpeg.Przeplywnosci[Math.Max(0, cmbQuality.SelectedIndex)];
        bool overwrite = chkOverwrite.Checked;
        bool multi = sel.Count > 1;
        var root = RootFolder;

        SetBusy(true);
        cts = new CancellationTokenSource();
        var ct = cts.Token;
        int ok = 0, skipped = 0, failed = 0, missing = 0;
        var temps = new List<string>();
        Log($"Start: {Opisy.Pliki(infos.Count)}, format: {cmbFormat.Text}" + (IsLossy ? $", jakość: {kbps} kb/s" : "") + $", folder docelowy: {outRoot}");
        try
        {
            for (int i = 0; i < infos.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var info = infos[i];
                var fileName = Path.GetFileName(info.Path);
                Status($"Plik {i + 1} z {infos.Count}: {fileName}");
                progress.Value = i * 1000 / infos.Count;

                var rel = Path.GetRelativePath(root, Path.GetDirectoryName(info.Path)!);
                var outDir = rel == "." ? outRoot : Path.Combine(outRoot, rel);
                var baseName = Path.GetFileNameWithoutExtension(info.Path);

                var jobs = new List<(AudioTrack t, string final, string temp)>();
                foreach (var g in sel)
                {
                    if (!info.ByKey.TryGetValue(g.Key, out var t))
                    {
                        Log($"„{fileName}”: brak ścieżki {g.ShortName()}.");
                        missing++;
                        continue;
                    }
                    var ext = Ffmpeg.Rozszerzenie(format, t.Codec);
                    var name = multi ? $"{baseName} - {g.ShortName()}" : baseName;
                    var final = Path.Combine(outDir, name + ext);
                    if (File.Exists(final) && !overwrite)
                    {
                        Log($"Pominięto, już istnieje: {Path.GetFileName(final)}");
                        skipped++;
                        continue;
                    }
                    jobs.Add((t, final, Path.Combine(outDir, name + ".czesciowy" + ext)));
                }
                if (jobs.Count == 0) continue;

                Directory.CreateDirectory(outDir);
                var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-loglevel", "error",
                    "-progress", "pipe:1", "-nostats", "-i", info.Path };
                foreach (var j in jobs)
                {
                    args.AddRange(new[] { "-map", $"0:{j.t.Index}", "-vn", "-sn", "-dn", "-map_chapters", "-1" });
                    args.AddRange(Ffmpeg.ArgumentyKodeka(format, j.t.Channels, stereo, kbps));
                    args.Add(j.temp);
                    temps.Add(j.temp);
                }

                int fileIdx = i;
                double dur = info.Duration;
                var (code, errors) = await Ffmpeg.RunAsync(args, sec =>
                {
                    if (dur <= 0) return;
                    var frac = Math.Clamp(sec / dur, 0, 1);
                    var v = (int)((fileIdx + frac) * 1000 / infos.Count);
                    BeginInvoke(() => { if (v > progress.Value && v <= 1000) progress.Value = v; });
                }, ct);

                if (code == 0)
                {
                    foreach (var j in jobs)
                    {
                        File.Move(j.temp, j.final, true);
                        temps.Remove(j.temp);
                        Log($"Zapisano: {Path.GetRelativePath(outRoot, j.final)}");
                        ok++;
                    }
                }
                else
                {
                    foreach (var j in jobs) { TryDelete(j.temp); temps.Remove(j.temp); }
                    failed += jobs.Count;
                    Log($"Błąd przy „{fileName}”: {(errors.Length > 0 ? errors : "kod " + code)}");
                }
            }
            progress.Value = 1000;
            var summary = $"Gotowe. Zapisano {Opisy.Ile(ok, "plik", "pliki", "plików")} audio" +
                          (skipped > 0 ? $", pominięto już istniejące: {skipped}" : "") +
                          (missing > 0 ? $", brakujące ścieżki: {missing}" : "") +
                          (failed > 0 ? $", błędy: {failed} (szczegóły w dzienniku)" : "") + ".";
            Status(summary);
            Log(summary);
            if (MessageBox.Show(this, summary + "\n\nOtworzyć folder docelowy?", Text, MessageBoxButtons.YesNo,
                    failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information) == DialogResult.Yes
                && Directory.Exists(outRoot))
                Process.Start(new ProcessStartInfo(outRoot) { UseShellExecute = true });
        }
        catch (OperationCanceledException)
        {
            Status($"Przerwano. Zapisano wcześniej {Opisy.Pliki(ok)}, niedokończony plik usunięto.");
            Log("Przerwano przez użytkownika.");
        }
        catch (Exception e)
        {
            Status("Wystąpił błąd: " + e.Message);
            Log("Błąd: " + e);
        }
        finally
        {
            foreach (var t in temps) TryDelete(t);
            cts?.Dispose();
            cts = null;
            SetBusy(false);
            btnStart.Focus();
        }
    }

    static void TryDelete(string f)
    {
        try { if (File.Exists(f)) File.Delete(f); } catch { }
    }
}

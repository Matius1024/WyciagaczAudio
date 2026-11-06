using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WyciagaczAudio;

public sealed class AudioTrack
{
    public int Index;          // bezwzględny numer strumienia w pliku (do -map 0:N)
    public string Codec = "";
    public int Channels;
    public string Language = "";
    public string Title = "";
    public bool IsAD;
    public bool IsDefault;
    public int Occurrence;     // która to ścieżka o tym samym opisie w pliku (1, 2...)
    public string Key = "";
}

public sealed class MediaInfo
{
    public string Path = "";
    public double Duration;
    public List<AudioTrack> Tracks = new();
    public Dictionary<string, AudioTrack> ByKey = new();
    public string? Error;
}

public sealed class TrackGroup
{
    public string Key = "";
    public string Language = "";
    public string Title = "";
    public bool IsAD;
    public bool IsDefault;
    public int Occurrence;
    public string Codec = "";
    public int Channels;
    public int Count;

    public string Describe(int totalFiles)
    {
        var sb = new StringBuilder(Opisy.Jezyk(Language));
        if (IsAD) sb.Append(", AUDIODESKRYPCJA");
        if (Title.Length > 0) sb.Append(", tytuł: ").Append(Title);
        if (Occurrence > 1) sb.Append($", {Occurrence}. ścieżka o takim opisie");
        sb.Append(" — ").Append(Opisy.Kodek(Codec)).Append(", ").Append(Opisy.Kanaly(Channels));
        if (IsDefault) sb.Append(", domyślna");
        if (totalFiles > 1) sb.Append($" — w {Count} z {totalFiles} plików");
        return sb.ToString();
    }

    public string ShortName()
    {
        var s = Opisy.Jezyk(Language);
        if (IsAD) s += " audiodeskrypcja";
        if (Title.Length > 0 && !IsAD) s += " " + Title;
        if (Occurrence > 1) s += " " + Occurrence;
        return Opisy.BezpiecznaNazwa(s);
    }
}

public enum OutFormat { Copy, Mp3, Aac, Flac, Wav }

public static class Opisy
{
    static readonly Dictionary<string, string> Jezyki = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pol"] = "polski", ["pl"] = "polski",
        ["eng"] = "angielski", ["en"] = "angielski",
        ["ger"] = "niemiecki", ["deu"] = "niemiecki", ["de"] = "niemiecki",
        ["fre"] = "francuski", ["fra"] = "francuski", ["fr"] = "francuski",
        ["spa"] = "hiszpański", ["es"] = "hiszpański",
        ["ita"] = "włoski", ["it"] = "włoski",
        ["rus"] = "rosyjski", ["ru"] = "rosyjski",
        ["ukr"] = "ukraiński", ["uk"] = "ukraiński",
        ["cze"] = "czeski", ["ces"] = "czeski", ["cs"] = "czeski",
        ["slo"] = "słowacki", ["slk"] = "słowacki", ["sk"] = "słowacki",
        ["jpn"] = "japoński", ["ja"] = "japoński",
        ["kor"] = "koreański", ["ko"] = "koreański",
        ["chi"] = "chiński", ["zho"] = "chiński", ["zh"] = "chiński",
        ["por"] = "portugalski", ["pt"] = "portugalski",
        ["dut"] = "niderlandzki", ["nld"] = "niderlandzki", ["nl"] = "niderlandzki",
        ["swe"] = "szwedzki", ["sv"] = "szwedzki",
        ["nor"] = "norweski", ["nob"] = "norweski", ["no"] = "norweski",
        ["dan"] = "duński", ["da"] = "duński",
        ["fin"] = "fiński", ["fi"] = "fiński",
        ["hun"] = "węgierski", ["hu"] = "węgierski",
        ["tur"] = "turecki", ["tr"] = "turecki",
        ["ara"] = "arabski", ["ar"] = "arabski",
        ["hin"] = "hindi", ["hi"] = "hindi",
        ["gre"] = "grecki", ["ell"] = "grecki", ["el"] = "grecki",
        ["rum"] = "rumuński", ["ron"] = "rumuński", ["ro"] = "rumuński",
        ["heb"] = "hebrajski", ["he"] = "hebrajski",
        ["tha"] = "tajski", ["th"] = "tajski",
        ["vie"] = "wietnamski", ["vi"] = "wietnamski",
        ["ind"] = "indonezyjski", ["id"] = "indonezyjski",
        ["hrv"] = "chorwacki", ["hr"] = "chorwacki",
        ["srp"] = "serbski", ["sr"] = "serbski",
        ["bul"] = "bułgarski", ["bg"] = "bułgarski",
        ["lit"] = "litewski", ["lt"] = "litewski",
        ["lav"] = "łotewski", ["lv"] = "łotewski",
        ["est"] = "estoński", ["et"] = "estoński",
        ["und"] = "język nieokreślony", [""] = "język nieokreślony",
    };

    public static string Jezyk(string code) =>
        Jezyki.TryGetValue(code, out var n) ? n : $"język {code}";

    public static bool JestPolski(string code) => Jezyk(code) == "polski";

    public static string Kodek(string c) => c switch
    {
        "aac" => "AAC",
        "ac3" => "Dolby Digital (AC-3)",
        "eac3" => "Dolby Digital Plus (E-AC-3)",
        "mp3" => "MP3",
        "opus" => "Opus",
        "vorbis" => "Vorbis",
        "flac" => "FLAC",
        "dts" => "DTS",
        "truehd" => "Dolby TrueHD",
        "alac" => "ALAC",
        _ when c.StartsWith("pcm_") => "PCM",
        _ => c.ToUpperInvariant(),
    };

    public static string Kanaly(int ch) => ch switch
    {
        1 => "mono",
        2 => "stereo",
        6 => "5.1",
        8 => "7.1",
        <= 0 => "liczba kanałów nieznana",
        _ => $"{ch} kanałów",
    };

    /// <summary>Liczba z poprawną polską odmianą, np. 1 plik, 3 pliki, 5 plików.</summary>
    public static string Ile(int n, string jeden, string kilka, string wiele)
    {
        string form = n == 1 ? jeden
            : n % 10 is >= 2 and <= 4 && n % 100 is not (>= 12 and <= 14) ? kilka
            : wiele;
        return $"{n} {form}";
    }

    public static string Pliki(int n) => Ile(n, "plik", "pliki", "plików");

    public static string BezpiecznaNazwa(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim().TrimEnd('.');
    }

    static readonly Regex AdRegex = new(
        @"audio\s*-?\s*deskryp|audiodeskryp|audio\s*-?\s*descri|descriptive|described|\bAD\b|opis\s+dla\s+niewidom|dla\s+niewidom|deskrypcj",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool WygladaNaAD(string text) => text.Length > 0 && AdRegex.IsMatch(text);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int StrCmpLogicalW(string a, string b);

    public static int PorownajNaturalnie(string a, string b) => StrCmpLogicalW(a, b);
}

public static class Ffmpeg
{
    public static readonly string[] VideoExt =
        { ".mp4", ".m4v", ".mkv", ".mov", ".avi", ".ts", ".m2ts", ".webm", ".wmv", ".mpg", ".mpeg", ".flv" };

    static string Find(string exe)
    {
        var local = Path.Combine(AppContext.BaseDirectory, exe);
        return File.Exists(local) ? local : exe;
    }

    public static string FfmpegExe => Find("ffmpeg.exe");
    public static string FfprobeExe => Find("ffprobe.exe");

    static ProcessStartInfo Psi(string exe, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    public static bool Dostepny(out string blad)
    {
        try
        {
            using var p = Process.Start(Psi(FfmpegExe, new[] { "-version" }))!;
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            using var q = Process.Start(Psi(FfprobeExe, new[] { "-version" }))!;
            q.StandardOutput.ReadToEnd();
            q.WaitForExit();
            blad = "";
            return true;
        }
        catch (Exception e)
        {
            blad = e.Message;
            return false;
        }
    }

    public static async Task<MediaInfo> ProbeAsync(string file, CancellationToken ct)
    {
        var info = new MediaInfo { Path = file };
        try
        {
            using var p = new Process { StartInfo = Psi(FfprobeExe, new[]
            {
                "-v", "error", "-print_format", "json", "-show_format", "-show_streams", "-select_streams", "a", file
            }) };
            p.Start();
            var outTask = p.StandardOutput.ReadToEndAsync(ct);
            var errTask = p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            var json = await outTask;
            var err = await errTask;
            if (p.ExitCode != 0) throw new Exception(err.Trim().Length > 0 ? err.Trim() : $"ffprobe zakończył się kodem {p.ExitCode}");
            Parse(json, info);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) { info.Error = e.Message; }
        return info;
    }

    static string Tag(JsonElement stream, string name)
    {
        if (!stream.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object) return "";
        foreach (var t in tags.EnumerateObject())
            if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                return t.Value.GetString()?.Trim() ?? "";
        return "";
    }

    static bool Disp(JsonElement stream, string name) =>
        stream.TryGetProperty("disposition", out var d) && d.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.Number && v.GetInt32() != 0;

    static void Parse(string json, MediaInfo info)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("format", out var fmt) && fmt.TryGetProperty("duration", out var dur) &&
            double.TryParse(dur.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            info.Duration = d;

        if (!root.TryGetProperty("streams", out var streams)) return;
        var occ = new Dictionary<string, int>();
        foreach (var s in streams.EnumerateArray())
        {
            var title = Tag(s, "title");
            var handler = Tag(s, "handler_name");
            // nazwy w stylu "SoundHandler" to śmieci kontenera mp4, nie tytuł
            if (title.Length == 0 && handler.Length > 0 && !handler.Contains("handler", StringComparison.OrdinalIgnoreCase))
                title = handler;
            var t = new AudioTrack
            {
                Index = s.GetProperty("index").GetInt32(),
                Codec = s.TryGetProperty("codec_name", out var c) ? c.GetString() ?? "" : "",
                Channels = s.TryGetProperty("channels", out var ch) ? ch.GetInt32() : 0,
                Language = Tag(s, "language"),
                Title = title,
                IsDefault = Disp(s, "default"),
            };
            t.IsAD = Disp(s, "visual_impaired") || Disp(s, "descriptions") || Opisy.WygladaNaAD(title) || Opisy.WygladaNaAD(handler);
            var baseKey = $"{t.Language.ToLowerInvariant()}|{t.Title.ToLowerInvariant()}|{t.IsAD}";
            occ[baseKey] = occ.GetValueOrDefault(baseKey) + 1;
            t.Occurrence = occ[baseKey];
            t.Key = baseKey + "|" + t.Occurrence;
            info.Tracks.Add(t);
            info.ByKey[t.Key] = t;
        }
    }

    public static List<TrackGroup> Grupuj(IEnumerable<MediaInfo> infos)
    {
        var map = new Dictionary<string, TrackGroup>();
        var order = new List<TrackGroup>();
        foreach (var info in infos)
            foreach (var t in info.Tracks)
            {
                if (!map.TryGetValue(t.Key, out var g))
                {
                    g = new TrackGroup
                    {
                        Key = t.Key, Language = t.Language, Title = t.Title, IsAD = t.IsAD, IsDefault = t.IsDefault,
                        Occurrence = t.Occurrence, Codec = t.Codec, Channels = t.Channels,
                    };
                    map[t.Key] = g;
                    order.Add(g);
                }
                g.Count++;
            }
        return order;
    }

    public static string RozszerzenieKopii(string codec) => codec switch
    {
        "aac" or "alac" => ".m4a",
        "ac3" => ".ac3",
        "eac3" => ".eac3",
        "mp3" => ".mp3",
        "mp2" => ".mp2",
        "opus" => ".opus",
        "vorbis" => ".ogg",
        "flac" => ".flac",
        "dts" => ".dts",
        "pcm_s16le" or "pcm_s24le" or "pcm_s32le" or "pcm_f32le" => ".wav",
        _ => ".mka",
    };

    public static string Rozszerzenie(OutFormat f, string codec) => f switch
    {
        OutFormat.Mp3 => ".mp3",
        OutFormat.Aac => ".m4a",
        OutFormat.Flac => ".flac",
        OutFormat.Wav => ".wav",
        _ => RozszerzenieKopii(codec),
    };

    /// <summary>Dostępne przepływności (kb/s) dla formatów stratnych.</summary>
    public static readonly int[] Przeplywnosci = { 64, 96, 128, 160, 192, 224, 256, 320 };

    /// <param name="kbps">Przepływność dla stereo; przy AAC wielokanałowym jest podwajana.</param>
    public static List<string> ArgumentyKodeka(OutFormat f, int channels, bool stereo, int kbps)
    {
        var a = new List<string>();
        bool doStereo = f != OutFormat.Copy && (stereo || (f == OutFormat.Mp3 && channels > 2));
        int outCh = doStereo ? 2 : channels;
        switch (f)
        {
            case OutFormat.Copy: a.AddRange(new[] { "-c:a", "copy" }); break;
            case OutFormat.Mp3: a.AddRange(new[] { "-c:a", "libmp3lame", "-b:a", $"{kbps}k" }); break;
            case OutFormat.Aac: a.AddRange(new[] { "-c:a", "aac", "-b:a", $"{(outCh > 2 ? kbps * 2 : kbps)}k" }); break;
            case OutFormat.Flac: a.AddRange(new[] { "-c:a", "flac" }); break;
            case OutFormat.Wav: a.AddRange(new[] { "-c:a", "pcm_s16le" }); break;
        }
        if (doStereo) a.AddRange(new[] { "-ac", "2" });
        return a;
    }

    /// <summary>Uruchamia ffmpeg; onProgress dostaje czas w sekundach. Zwraca (kod, komunikaty błędów).</summary>
    public static async Task<(int Code, string Errors)> RunAsync(IEnumerable<string> args, Action<double>? onProgress, CancellationToken ct)
    {
        using var p = new Process { StartInfo = Psi(FfmpegExe, args) };
        var err = new StringBuilder();
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null || onProgress == null) return;
            // ffmpeg podaje out_time_us i (myląco) out_time_ms – oba w mikrosekundach
            int eq = e.Data.IndexOf('=');
            if (eq < 0) return;
            var key = e.Data[..eq];
            if ((key == "out_time_us" || key == "out_time_ms") &&
                long.TryParse(e.Data[(eq + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var us) && us >= 0)
                onProgress(us / 1_000_000.0);
        };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            try { p.WaitForExit(10000); } catch { }
            throw;
        }
        p.WaitForExit();
        lock (err) return (p.ExitCode, err.ToString().Trim());
    }
}

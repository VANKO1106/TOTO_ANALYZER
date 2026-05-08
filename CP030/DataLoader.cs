using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TotoAnalyzer.Models;

namespace TotoAnalyzer;

/// <summary>
/// Downloads and parses historical Toto 6/49 draw files from info.toto.bg.
/// Includes local caching to prevent server blocks and handles both legacy and modern formats.
/// </summary>
public sealed class DataLoader
{
    private const string BaseUrl = "https://info.toto.bg";
    private const string PageUrl = "https://info.toto.bg/statistika/6x49";

    private static readonly HttpClient Http = BuildClient();

    private static HttpClient BuildClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 TotoAnalyzer/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,*/*");
        return client;
    }

    // ── Regex ─────────────────────────────────────────────────────────────────

    private static readonly Regex LinkRx = new(
        @"href=[""']([^""']*?\.(?:txt|docx))[""'][^>]*?>(\d{4})<",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// Updated to handle "Тираж N/Year" format found in newer files
    private static readonly Regex SerialRx = new(
        @"^(?:Тираж\s+)?(\d+)(?:/\d+)?\s*[-,:]\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NumRx = new(
        @"(?<!\d)(0?[1-9]|[1-3]\d|4[0-9])(?!\d)",
        RegexOptions.Compiled);

    /// Detects year markers within the file content (post-2017 style)
    private static readonly Regex YearInTextRx = new(
        @"за (\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<FileEntry>> DiscoverFilesAsync()
    {
        string html;
        try { html = await Http.GetStringAsync(PageUrl); }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Грешка при свързване с {PageUrl}: {ex.Message}", ex);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<FileEntry>();

        foreach (Match m in LinkRx.Matches(html))
        {
            string url = ToAbsoluteUrl(m.Groups[1].Value);
            string yStr = m.Groups[2].Value;

            if (!seen.Add(url)) continue;
            if (!int.TryParse(yStr, out int year)) continue;

            result.Add(new FileEntry(url, year));
        }

        return result.OrderBy(f => f.Year).ToList();
    }

    public async Task<IEnumerable<Draw>> LoadDrawsAsync(int fromYear, int toYear, IReadOnlyList<FileEntry>? files = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        files ??= await DiscoverFilesAsync();

        var toLoad = files.Where(f => f.Year >= fromYear && f.Year <= toYear).ToList();
        if (toLoad.Count == 0) return Enumerable.Empty<Draw>();

        var all = new List<Draw>();
        foreach (var entry in toLoad)
        {
            var draws = await LoadOneFileAsync(entry);
            all.AddRange(draws);
        }

        return all.OrderBy(d => d.Year).ThenBy(d => d.DrawNumber);
    }

    // ── File dispatching with Caching ─────────────────────────────────────────

    private async Task<IReadOnlyList<Draw>> LoadOneFileAsync(FileEntry entry)
    {
        string fileName = Path.GetFileName(entry.Url);
        int queryIndex = fileName.IndexOf('?');
        if (queryIndex > 0) fileName = fileName.Substring(0, queryIndex);

        string localPath = Path.Combine("DataCache", fileName);
        byte[] fileData;

        Directory.CreateDirectory("DataCache");

        try
        {
            if (File.Exists(localPath))
            {
                fileData = await File.ReadAllBytesAsync(localPath);
                Colour($"  ○ {entry.Year}  (от кеш)", ConsoleColor.DarkGray);
            }
            else
            {
                Colour($"  ↓ {entry.Year}  {entry.Url}", ConsoleColor.Gray);
                fileData = await Http.GetByteArrayAsync(entry.Url);

                if (IsHtml(fileData))
                    throw new InvalidDataException("Сървърът върна HTML вместо файл.");

                await File.WriteAllBytesAsync(localPath, fileData);
                await Task.Delay(350);
            }

            var draws = entry.Url.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                ? ParseDocxFromBytes(fileData, entry.Year)
                : ParseTxtFromBytes(fileData, entry.Year);

            Colour($"    → {draws.Count} тиража", ConsoleColor.Green);
            return draws;
        }
        catch (Exception ex)
        {
            Colour($"    ✗ {entry.Year}: {ex.Message}", ConsoleColor.Yellow);
            return Array.Empty<Draw>();
        }
    }

    private List<Draw> ParseTxtFromBytes(byte[] bytes, int year)
    {
        string text = Decode(bytes);
        return ParseLines(text.Split('\n'), year);
    }

    private List<Draw> ParseDocxFromBytes(byte[] bytes, int year)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);

        var body = doc.MainDocumentPart?.Document?.Body ?? throw new InvalidDataException("Празен DOCX документ.");

        var lines = body.Descendants<Paragraph>().Select(p => p.InnerText)
            .Concat(body.Descendants<TableRow>().Select(r => string.Join("\t", r.Descendants<TableCell>().Select(c => c.InnerText))));

        return ParseLines(lines, year);
    }

    // ── Core line parser ──────────────────────────────────────────────────────

    private List<Draw> ParseLines(IEnumerable<string> rawLines, int defaultYear)
    {
        var draws = new List<Draw>();
        int activeYear = defaultYear;
        int drawIndex = 0;

        foreach (var raw in rawLines)
        {
            string line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Check if line contains a year update (e.g., "за 2022")
            var yearMatch = YearInTextRx.Match(line);
            if (yearMatch.Success) int.TryParse(yearMatch.Groups[1].Value, out activeYear);

            string[] groups = SplitIntoGroups(line);

            foreach (string grp in groups)
            {
                string text = grp.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                int serial = 0;
                var sm = SerialRx.Match(text);
                if (sm.Success)
                {
                    int.TryParse(sm.Groups[1].Value, out serial);
                    text = text[sm.Length..];
                }

                int[]? nums = Extract6(text);
                if (nums is null) continue;

                // Dates: Jan-1 + drawIndex * 3.5 days (approx twice weekly)
                var baseDate = new DateTime(activeYear, 1, 1);
                var date = baseDate.AddDays(drawIndex * 3.5);

                draws.Add(new Draw
                {
                    DrawNumber = serial > 0 ? serial : drawIndex + 1,
                    Date = date.Year == activeYear ? date : new DateTime(activeYear, 12, 31),
                    Numbers = nums
                });

                drawIndex++;
            }
        }

        return draws;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string[] SplitIntoGroups(string line)
    {
        if (line.Contains('\t')) return line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        return Regex.Split(line, @"\s{2,}");
    }

    private int[]? Extract6(string text)
    {
        var nums = new List<int>(6);
        foreach (Match m in NumRx.Matches(text))
        {
            if (int.TryParse(m.Value, out int n) && n is >= 1 and <= 49)
                nums.Add(n);
            if (nums.Count > 6) return null;
        }
        return nums.Count == 6 ? nums.OrderBy(x => x).ToArray() : null;
    }

    private static bool IsHtml(byte[] b)
    {
        var s = Encoding.ASCII.GetString(b, 0, Math.Min(512, b.Length)).ToLowerInvariant();
        return s.Contains("<!doctype") || s.Contains("<html");
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes); }
        catch { return Encoding.GetEncoding(1251).GetString(bytes); }
    }

    private static string ToAbsoluteUrl(string href) =>
        href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : BaseUrl + (href.StartsWith('/') ? href : "/" + href);

    private static void Colour(string msg, ConsoleColor c)
    { Console.ForegroundColor = c; Console.WriteLine(msg); Console.ResetColor(); }
}

/// <summary>
/// Definition moved here to ensure scope availability as per source file structure.
/// </summary>
public sealed record FileEntry(string Url, int Year);
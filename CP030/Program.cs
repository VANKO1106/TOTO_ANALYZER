using System.Text;
using TotoAnalyzer;
using TotoAnalyzer.Models;

Console.OutputEncoding = Encoding.UTF8;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Console.Title = "Тото Анализатор 6/49";

var loader     = new DataLoader();
var visualizer = new Visualizer();

Statistics? stats    = null;
int         fromYear = 0;
int         toYear   = 0;

IReadOnlyList<FileEntry> availableFiles;

// ── Initial discovery ─────────────────────────────────────────────────────────
PrintBanner();
Colour("  Свързване с info.toto.bg — моля изчакайте...", ConsoleColor.DarkGray);

try
{
    availableFiles = await loader.DiscoverFilesAsync();

    if (availableFiles.Count == 0)
        throw new InvalidOperationException("На страницата не са намерени файлове с данни.");

    Colour($"  ✓ Намерени {availableFiles.Count} файла  " +
           $"({availableFiles.Min(f => f.Year)}–{availableFiles.Max(f => f.Year)})",
           ConsoleColor.Green);
}
catch (Exception ex)
{
    Colour($"\n  ✗ Неуспешно зареждане: {ex.Message}", ConsoleColor.Red);
    Colour("  Проверете интернет връзката и рестартирайте приложението.", ConsoleColor.Yellow);
    Pause();
    return;
}

Pause();

// ── Main menu loop ────────────────────────────────────────────────────────────
while (true)
{
    Console.Clear();
    PrintMenu(fromYear, toYear, stats);

    switch (Console.ReadLine()?.Trim())
    {
        case "1": await HandleSelectPeriodAsync(); break;
        case "2": HandleTopNumbers();              break;
        case "3": HandleHotPairs();                break;
        case "4": HandleDecadeDistribution();      break;
        case "0":
            Colour("\n  Довиждане!\n", ConsoleColor.Cyan);
            return;
        default:
            Colour("\n  Невалиден избор — въведете 0–4.", ConsoleColor.Red);
            Pause();
            break;
    }
}

// ── Handlers ─────────────────────────────────────────────────────────────────

async Task HandleSelectPeriodAsync()
{
    int minY = availableFiles.Min(f => f.Year);
    int maxY = availableFiles.Max(f => f.Year);

    Console.WriteLine($"\n  Налични години: {minY}–{maxY}  ({availableFiles.Count} файла)");

    int fy = ReadInt($"  От година  [{minY}]: ", minY, maxY, minY);
    int ty = ReadInt($"  До година  [{maxY}]: ", fy,  maxY, maxY);

    Console.WriteLine($"\n  Изтегляне на данни за {fy}–{ty} ...\n");

    try
    {
        var draws = (await loader.LoadDrawsAsync(fy, ty, availableFiles)).ToList();

        if (draws.Count == 0)
        {
            Colour("\n  ⚠  Намерени 0 тиража. Проверете интернет връзката.", ConsoleColor.Yellow);
        }
        else
        {
            fromYear = fy;
            toYear   = ty;
            stats    = new Statistics(draws);
            Colour($"\n  ✓ Заредени {draws.Count} тиража за {fy}–{ty}.", ConsoleColor.Green);
        }
    }
    catch (Exception ex)
    {
        Colour($"\n  ✗ Грешка при зареждане: {ex.Message}", ConsoleColor.Red);
    }

    Pause();
}

void HandleTopNumbers()
{
    if (!EnsureLoaded()) return;

    Console.WriteLine();
    Colour("  Показва кои числа са излизали най-много пъти в избрания период.\n",
           ConsoleColor.DarkGray);

    int n = ReadInt("  Брой числа N [10]: ", 1, 49, 10);
    visualizer.ShowTopNumbersBarChart(stats!.GetTopNumbers(n), fromYear, toYear);
    Pause();
}

void HandleHotPairs()
{
    if (!EnsureLoaded()) return;

    Console.WriteLine();
    Colour("  Двойки числа, излизали най-често заедно в един тираж.\n",
           ConsoleColor.DarkGray);

    int n = ReadInt("  Брой двойки N [10]: ", 1, 200, 10);
    visualizer.ShowHotPairs(stats!.GetHotPairs(n));
    Pause();
}

void HandleDecadeDistribution()
{
    if (!EnsureLoaded()) return;

    Console.WriteLine();
    Colour("  Разпределение по диапазони (десетици) + топлинна карта 7×7.\n",
           ConsoleColor.DarkGray);

    visualizer.ShowDecadeBarChart(stats!.GetDistributionByDecade());
    visualizer.ShowHeatMap(stats!.GetAllFrequencies());
    Pause();
}

// ── UI helpers ────────────────────────────────────────────────────────────────

void PrintBanner()
{
    Colour("\n  ╔══════════════════════════════════════════╗", ConsoleColor.Cyan);
    Colour("  ║        Т О Т О   А Н А Л И З А Т О Р     ║", ConsoleColor.Cyan);
    Colour("  ║                 6 / 4 9                  ║", ConsoleColor.Cyan);
    Colour("  ╚══════════════════════════════════════════╝\n", ConsoleColor.Cyan);
}

void PrintMenu(int fy, int ty, Statistics? st)
{
    Colour("  ╔══════════════════════════════════════════╗", ConsoleColor.Cyan);
    Colour("  ║          ТОТО АНАЛИЗАТОР  6/49           ║", ConsoleColor.Cyan);
    Colour("  ╠══════════════════════════════════════════╣", ConsoleColor.Cyan);

    if (st != null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        string info = $"  Период: {fy}–{ty}  │  Тиражи: {st.TotalDraws}";
        Console.WriteLine($"  ║ {info.PadRight(40)} ║");
        Colour("  ╠══════════════════════════════════════════╣", ConsoleColor.Cyan);
    }

    Console.ResetColor();
    Console.WriteLine("  ║                                          ║");
    Console.WriteLine("  ║   [1]  Избери период (от год. до год.)   ║");
    Console.WriteLine("  ║   [2]  Топ N най-чести числа             ║");
    Console.WriteLine("  ║   [3]  Горещи двойки                     ║");
    Console.WriteLine("  ║   [4]  Разпределение по десетици         ║");
    Console.WriteLine("  ║                                          ║");
    Colour("  ║   [0]  Изход                             ║", ConsoleColor.Red);
    Colour("  ╚══════════════════════════════════════════╝", ConsoleColor.Cyan);
    Console.Write("   Избор: ");
}

int ReadInt(string prompt, int min, int max, int defaultVal)
{
    while (true)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input)) return defaultVal;
        if (int.TryParse(input, out int v) && v >= min && v <= max) return v;
        Colour($"  Грешка: въведете цяло число между {min} и {max}.", ConsoleColor.Red);
    }
}

bool EnsureLoaded()
{
    if (stats != null) return true;
    Colour("\n  Моля, първо изберете период с опция [1].", ConsoleColor.Yellow);
    Pause();
    return false;
}

void Pause()
{
    Colour("\n  Натиснете произволен клавиш...", ConsoleColor.DarkGray);
    Console.ReadKey(true);
}

void Colour(string msg, ConsoleColor c)
{ Console.ForegroundColor = c; Console.WriteLine(msg); Console.ResetColor(); }

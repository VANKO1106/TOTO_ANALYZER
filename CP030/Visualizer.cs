namespace TotoAnalyzer;

/// <summary>
/// Renders ASCII visualisations of statistical results to the console.
/// Provides two main visualisations:
///   1. Horizontal bar chart (normalised '#' bars)
///   2. 7×7 heat map coloured by frequency percentile
/// </summary>
public sealed class Visualizer
{
    private const int BarMaxWidth = 42;   // max bar length in characters

    // ── Visualisation 1: Horizontal Bar Chart ────────────────────────────────

    /// <summary>
    /// Draws a horizontal bar chart for the top-N most frequent lottery numbers.
    /// Bar width is normalised against the maximum frequency in the set.
    /// </summary>
    public void ShowTopNumbersBarChart(
        Dictionary<int, int> topNumbers,
        int fromYear,
        int toYear)
    {
        if (topNumbers.Count == 0) { PrintEmpty(); return; }

        int maxVal = topNumbers.Values.Max();

        Console.WriteLine();
        PrintSectionHeader($"  Топ {topNumbers.Count} числа  ({fromYear}–{toYear}):");
        Console.WriteLine();

        foreach (var (number, count) in topNumbers)
        {
            int barLen = NormaliseBar(count, maxVal);
            string bar = new('#', barLen);

            // Number label
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"  {number,3} │ ");

            // Bar
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{bar.PadRight(BarMaxWidth)}");

            // Count
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" {count}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Draws a horizontal bar chart for decade (range) distribution.
    /// </summary>
    public void ShowDecadeBarChart(Dictionary<string, int> distribution)
    {
        if (distribution.Count == 0) { PrintEmpty(); return; }

        int maxVal = distribution.Values.Max();

        Console.WriteLine();
        PrintSectionHeader("  Разпределение по диапазони:");
        Console.WriteLine();

        foreach (var (range, count) in distribution)
        {
            int barLen = NormaliseBar(count, maxVal);
            string bar = new('#', barLen);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"  {range,5} │ ");

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"{bar.PadRight(BarMaxWidth)}");

            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" {count}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    // ── Visualisation 2: 7×7 Heat Map ────────────────────────────────────────

    /// <summary>
    /// Displays numbers 1–49 in a 7×7 grid coloured by draw frequency:
    ///   • Top 30%    → Red    (горещи  / hot)
    ///   • Middle 40% → Yellow (неутрални / neutral)
    ///   • Bottom 30% → Cyan   (студени / cold)
    /// </summary>
    public void ShowHeatMap(Dictionary<int, int> allFrequencies)
    {
        if (allFrequencies.Count == 0) { PrintEmpty(); return; }

        // Determine percentile thresholds on the sorted frequency values
        var sorted = allFrequencies.Values.OrderBy(v => v).ToList();
        int p30 = sorted[Math.Max(0, (int)(sorted.Count * 0.30) - 1)];
        int p70 = sorted[Math.Max(0, (int)(sorted.Count * 0.70) - 1)];

        Console.WriteLine();
        PrintSectionHeader("  Топлинна карта (1–49):");
        Console.WriteLine();

        for (int row = 0; row < 7; row++)
        {
            Console.Write("    ");
            for (int col = 0; col < 7; col++)
            {
                int num = row * 7 + col + 1;

                if (num > 49)
                {
                    Console.Write("     ");
                    continue;
                }

                int freq = allFrequencies.GetValueOrDefault(num, 0);

                Console.ForegroundColor = FrequencyColor(freq, p30, p70);
                Console.Write($" {num,2}");
                Console.ResetColor();
                Console.Write("  ");
            }
            Console.WriteLine();
        }

        Console.WriteLine();

        // Colour legend
        Console.Write("  Легенда:  ");
        WriteColoured("● Горещи (топ 30%)",    ConsoleColor.Red);
        Console.Write("   ");
        WriteColoured("● Неутрални (40%)",      ConsoleColor.Yellow);
        Console.Write("   ");
        WriteColoured("● Студени (долни 30%)",  ConsoleColor.Cyan);
        Console.WriteLine();
        Console.WriteLine();
    }

    // ── Hot pairs list ────────────────────────────────────────────────────────

    /// <summary>
    /// Displays a ranked list of the hottest number pairs.
    /// </summary>
    public void ShowHotPairs(List<(int Num1, int Num2, int Count)> pairs)
    {
        if (pairs.Count == 0) { PrintEmpty(); return; }

        Console.WriteLine();
        PrintSectionHeader("  Горещи двойки числа:");
        Console.WriteLine();

        int maxCount = pairs.Max(p => p.Count);

        for (int i = 0; i < pairs.Count; i++)
        {
            var (n1, n2, count) = pairs[i];
            int barLen = NormaliseBar(count, maxCount, 20);
            string bar = new('#', barLen);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  {i + 1,3}. ");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"({n1,2}, {n2,2})");

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"  {bar.PadRight(20)}");

            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" {count} пъти заедно");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static ConsoleColor FrequencyColor(int freq, int p30, int p70) =>
        freq >= p70 ? ConsoleColor.Red :
        freq >= p30 ? ConsoleColor.Yellow :
                      ConsoleColor.Cyan;

    private static int NormaliseBar(int value, int max, int maxWidth = BarMaxWidth) =>
        max == 0 ? 0 : (int)Math.Round((double)value / max * maxWidth);

    private static void PrintSectionHeader(string title)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(title);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  " + new string('─', BarMaxWidth + 14));
        Console.ResetColor();
    }

    private static void WriteColoured(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    private static void PrintEmpty()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  (Няма данни за показване)");
        Console.ResetColor();
    }
}

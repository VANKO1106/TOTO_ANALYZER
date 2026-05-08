using TotoAnalyzer.Models;

namespace TotoAnalyzer;

/// <summary>
/// Provides statistical analysis over a collection of lottery draws.
/// All analytical methods are implemented exclusively with LINQ.
/// </summary>
public sealed class Statistics
{
    // Materialise once so IEnumerable is not re-enumerated on every call.
    private readonly IReadOnlyList<Draw> _draws;

    public Statistics(IEnumerable<Draw> draws)
    {
        ArgumentNullException.ThrowIfNull(draws);
        _draws = draws.ToList();
    }

    // ── Summary properties ───────────────────────────────────────────────────

    public int TotalDraws => _draws.Count;
    public int MinYear    => _draws.Any() ? _draws.Min(d => d.Year) : 0;
    public int MaxYear    => _draws.Any() ? _draws.Max(d => d.Year) : 0;

    // ── LINQ Method 1: Top N most frequent numbers ───────────────────────────

    /// <summary>
    /// Returns the N most frequently drawn numbers, sorted descending by count.
    /// Key = lottery number (1–49), Value = number of appearances.
    /// </summary>
    public Dictionary<int, int> GetTopNumbers(int n) =>
        _draws
            .SelectMany(d => d.Numbers)
            .GroupBy(num => num)
            .OrderByDescending(g => g.Count())
            .Take(n)
            .ToDictionary(g => g.Key, g => g.Count());

    // ── LINQ Method 2: Hot pairs ─────────────────────────────────────────────

    /// <summary>
    /// Finds the N pairs of numbers that most frequently appear together
    /// in the same draw.
    /// Returns (number1, number2, co-occurrence count) tuples.
    /// </summary>
    public List<(int Num1, int Num2, int Count)> GetHotPairs(int n) =>
        (from draw in _draws
         // Generate all C(6,2) = 15 unordered pairs per draw
         from i in Enumerable.Range(0, draw.Numbers.Length)
         from j in Enumerable.Range(i + 1, draw.Numbers.Length - i - 1)
         // Numbers are pre-sorted, so Numbers[i] < Numbers[j] always
         let pair = (draw.Numbers[i], draw.Numbers[j])
         group pair by pair into g
         orderby g.Count() descending
         select (g.Key.Item1, g.Key.Item2, g.Count()))
        .Take(n)
        .ToList();

    // ── LINQ Method 3: Distribution by decade ────────────────────────────────

    /// <summary>
    /// Groups all drawn numbers into five ranges: 1-10, 11-20, 21-30, 31-40, 41-49.
    /// Returns a dictionary with range label as key and total draw count as value.
    /// </summary>
    public Dictionary<string, int> GetDistributionByDecade()
    {
        // Define the five decade ranges
        var ranges = new[]
        {
            ( Label: "1-10",  Lo:  1, Hi: 10 ),
            ( Label: "11-20", Lo: 11, Hi: 20 ),
            ( Label: "21-30", Lo: 21, Hi: 30 ),
            ( Label: "31-40", Lo: 31, Hi: 40 ),
            ( Label: "41-49", Lo: 41, Hi: 49 )
        };

        return
            (from draw in _draws
             from number in draw.Numbers
             let range = ranges.First(r => number >= r.Lo && number <= r.Hi)
             group number by range.Label into g
             // Preserve the natural order of the ranges (by first number of range)
             orderby int.Parse(g.Key.Split('-')[0])
             select g)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    // ── Helper: frequency map for all 49 numbers (used by heat map) ──────────

    /// <summary>
    /// Returns how many times each number (1–49) was drawn.
    /// All 49 numbers are present in the result (zero if never drawn).
    /// </summary>
    public Dictionary<int, int> GetAllFrequencies()
    {
        var freq = _draws
            .SelectMany(d => d.Numbers)
            .GroupBy(n => n)
            .ToDictionary(g => g.Key, g => g.Count());

        // Ensure every number 1–49 has an entry
        foreach (int n in Enumerable.Range(1, 49))
            freq.TryAdd(n, 0);

        return freq;
    }
}

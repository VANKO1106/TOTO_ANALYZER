namespace TotoAnalyzer.Models;

/// <summary>
/// Represents a single 6/49 lottery draw.
/// Numbers are always stored sorted ascending (1–49).
/// </summary>
public sealed class Draw
{
    public int      DrawNumber { get; init; }
    public DateTime Date       { get; init; }
    public int[]    Numbers    { get; init; } = Array.Empty<int>();
    public int      Year       => Date.Year;

    public override string ToString() =>
        $"[{DrawNumber,5}]  {Date:dd.MM.yyyy}  →  " +
        string.Join("  ", Numbers.Select(n => n.ToString("D2")));
}

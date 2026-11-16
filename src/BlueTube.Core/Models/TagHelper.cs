namespace BlueTube.Core.Models;

/// <summary>SQLite has no list type. Tags are kept as one lowercase, comma-separated column.</summary>
public static class TagHelper
{
    public static IReadOnlyList<string> Parse(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(t => t.ToLowerInvariant())
                 .Distinct()
                 .ToList();

    public static string Join(IEnumerable<string> tags) =>
        string.Join(",", Parse(string.Join(",", tags)));
}

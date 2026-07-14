namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemCatalog
{
    private readonly Dictionary<string, IReadOnlyList<PathOfExileTradeItemEntry>> byDisplayText;

    public PathOfExileTradeItemCatalog(
        IEnumerable<PathOfExileTradeItemEntry> entries,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries = entries
            .OrderBy(entry => entry.ProviderOrder)
            .ToArray();
        Diagnostics = diagnostics ?? [];

        byDisplayText = Entries
            .SelectMany(entry => DisplayTexts(entry).Select(text => new { Text = text, Entry = entry }))
            .GroupBy(pair => pair.Text, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PathOfExileTradeItemEntry>)group
                    .Select(pair => pair.Entry)
                    .Distinct()
                    .ToArray(),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<PathOfExileTradeItemEntry> Entries { get; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; }

    public IReadOnlyList<PathOfExileTradeItemEntry> FindByExactDisplayText(string? displayText)
    {
        return !string.IsNullOrWhiteSpace(displayText) &&
            byDisplayText.TryGetValue(displayText.Trim(), out var entries)
            ? entries
            : [];
    }

    private static IEnumerable<string> DisplayTexts(PathOfExileTradeItemEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Name))
        {
            yield return entry.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry.Type))
        {
            yield return entry.Type.Trim();
        }
    }
}

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeLeagueCatalog
{
    public PathOfExileTradeLeagueCatalog(
        IReadOnlyList<PathOfExileTradeLeagueEntry> entries,
        DateTimeOffset retrievedAtUtc,
        DateTimeOffset freshUntilUtc,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? diagnostics = null)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        RetrievedAtUtc = retrievedAtUtc;
        FreshUntilUtc = freshUntilUtc;
        Diagnostics = diagnostics ?? [];
    }

    public IReadOnlyList<PathOfExileTradeLeagueEntry> Entries { get; }

    public IReadOnlyList<PathOfExileTradeLeagueEntry> PcEntries => Entries
        .Where(entry => string.Equals(entry.Realm, "pc", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public DateTimeOffset RetrievedAtUtc { get; }

    public DateTimeOffset FreshUntilUtc { get; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; }

    public bool IsFresh(DateTimeOffset utcNow) => utcNow < FreshUntilUtc;
}

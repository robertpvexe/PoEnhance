namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeLeaguesResponseParseResult
{
    public bool IsSuccess => Entries is not null;

    public IReadOnlyList<PathOfExileTradeLeagueEntry>? Entries { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; init; } = [];
}

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeLeagueResolutionResult
{
    public bool IsSuccess => League is not null;

    public PathOfExileTradeLeagueEntry? League { get; init; }

    public IReadOnlyList<PathOfExileTradeHttpDiagnostic> Diagnostics { get; init; } = [];

    public bool IsCancelled { get; init; }
}

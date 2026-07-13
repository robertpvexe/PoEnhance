namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeRateLimitParseResult
{
    public bool IsSuccess => Snapshot is not null;

    public PathOfExileTradeRateLimitSnapshot? Snapshot { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeRateLimitParseResult Success(
        PathOfExileTradeRateLimitSnapshot snapshot,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics)
    {
        return new PathOfExileTradeRateLimitParseResult
        {
            Snapshot = snapshot,
            Diagnostics = diagnostics,
        };
    }
}

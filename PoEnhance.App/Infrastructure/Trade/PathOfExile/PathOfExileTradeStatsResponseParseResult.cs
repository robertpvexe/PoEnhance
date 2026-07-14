namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatsResponseParseResult
{
    public bool IsSuccess { get; init; }

    public PathOfExileTradeStatCatalog? Catalog { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeStatsResponseParseResult Success(
        PathOfExileTradeStatCatalog catalog,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics)
    {
        return new PathOfExileTradeStatsResponseParseResult
        {
            IsSuccess = true,
            Catalog = catalog,
            Diagnostics = diagnostics,
        };
    }

    public static PathOfExileTradeStatsResponseParseResult Failure(
        params PathOfExileTradeQueryDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeStatsResponseParseResult
        {
            IsSuccess = false,
            Diagnostics = diagnostics,
        };
    }
}

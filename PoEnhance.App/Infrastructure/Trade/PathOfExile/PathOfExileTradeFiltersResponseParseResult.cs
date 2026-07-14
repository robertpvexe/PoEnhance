namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeFiltersResponseParseResult
{
    public bool IsSuccess => Catalog is not null;

    public PathOfExileTradeFilterCatalog? Catalog { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeFiltersResponseParseResult Success(
        PathOfExileTradeFilterCatalog catalog,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics)
    {
        return new PathOfExileTradeFiltersResponseParseResult
        {
            Catalog = catalog,
            Diagnostics = diagnostics,
        };
    }

    public static PathOfExileTradeFiltersResponseParseResult Failure(
        params PathOfExileTradeQueryDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeFiltersResponseParseResult
        {
            Diagnostics = diagnostics,
        };
    }
}

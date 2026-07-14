namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeItemsResponseParseResult
{
    public bool IsSuccess => Catalog is not null;

    public PathOfExileTradeItemCatalog? Catalog { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeItemsResponseParseResult Success(
        PathOfExileTradeItemCatalog catalog,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics)
    {
        return new PathOfExileTradeItemsResponseParseResult
        {
            Catalog = catalog,
            Diagnostics = diagnostics,
        };
    }

    public static PathOfExileTradeItemsResponseParseResult Failure(
        params PathOfExileTradeQueryDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeItemsResponseParseResult
        {
            Diagnostics = diagnostics,
        };
    }
}

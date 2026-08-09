namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeLeagueCatalogProviderResult
{
    public bool IsSuccess => Catalog is not null;

    public PathOfExileTradeLeagueCatalog? Catalog { get; init; }

    public IReadOnlyList<PathOfExileTradeHttpDiagnostic> Diagnostics { get; init; } = [];

    public bool IsCancelled { get; init; }
}

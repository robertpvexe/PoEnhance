namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeFilterCatalogProvider
{
    bool TryGetCachedCatalog(out PathOfExileTradeFilterCatalog catalog)
    {
        catalog = null!;
        return false;
    }

    Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}

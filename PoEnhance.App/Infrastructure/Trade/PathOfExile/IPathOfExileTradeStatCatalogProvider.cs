namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeStatCatalogProvider
{
    bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog catalog)
    {
        catalog = null!;
        return false;
    }

    Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeFilterCatalogProvider
{
    Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}

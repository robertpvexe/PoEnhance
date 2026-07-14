namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeStatCatalogProvider
{
    Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}

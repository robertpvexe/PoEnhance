namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeItemCatalogProvider
{
    Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}

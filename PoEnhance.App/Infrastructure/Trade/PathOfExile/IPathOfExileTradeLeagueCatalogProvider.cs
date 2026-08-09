namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeLeagueCatalogProvider
{
    Task<PathOfExileTradeLeagueCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}

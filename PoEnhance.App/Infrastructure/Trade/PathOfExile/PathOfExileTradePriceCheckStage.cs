namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal enum PathOfExileTradePriceCheckStage
{
    QueryBuild,
    CatalogLoad,
    ModifierMapping,
    Search,
    Fetch,
    Completed,
}

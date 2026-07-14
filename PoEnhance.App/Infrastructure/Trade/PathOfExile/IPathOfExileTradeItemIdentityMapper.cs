using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeItemIdentityMapper
{
    PathOfExileTradeItemIdentityMappingResult Map(
        TradeSearchDraft? draft,
        PathOfExileTradeItemCatalog? catalog);
}

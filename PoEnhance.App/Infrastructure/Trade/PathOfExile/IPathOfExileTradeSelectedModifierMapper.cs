using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeSelectedModifierMapper
{
    PathOfExileTradeSelectedModifierMappingResult Map(TradeSearchDraft? draft);
}

using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeSelectedModifierMapper
{
    PathOfExileTradeSelectedModifierMappingResult Map(
        IReadOnlyList<TradeModifierFilterDraft>? modifierFilters,
        PathOfExileTradeStatCatalog? catalog);
}

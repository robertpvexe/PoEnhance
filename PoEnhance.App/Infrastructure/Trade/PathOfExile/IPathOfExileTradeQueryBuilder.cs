using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeQueryBuilder
{
    PathOfExileTradeQueryBuildResult Build(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters = null,
        PathOfExileTradeItemIdentity? providerItemIdentity = null,
        PathOfExileTradeFilterCatalog? providerFilterCatalog = null,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? selectedItemPropertyFilters = null);
}

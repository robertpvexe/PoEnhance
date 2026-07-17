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

    PathOfExileTradeQueryBuildResult Build(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters,
        PathOfExileTradeItemIdentity? providerItemIdentity,
        PathOfExileTradeFilterCatalog? providerFilterCatalog,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? selectedItemPropertyFilters,
        IReadOnlyList<PathOfExileTradeSelectedRequestedItemFilter>? selectedRequestedItemFilters)
    {
        return Build(
            draft,
            validationResult,
            leagueIdentifier,
            selectedModifierFilters,
            providerItemIdentity,
            providerFilterCatalog,
            selectedItemPropertyFilters);
    }
}

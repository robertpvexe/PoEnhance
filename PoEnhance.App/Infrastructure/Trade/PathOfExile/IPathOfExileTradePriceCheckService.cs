using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradePriceCheckService
{
    Task<PathOfExileTradeFilterCatalogProviderResult> InitializeFilterCatalogAsync(
        CancellationToken cancellationToken = default);

    TradeSearchDraft ResolveEffectiveDraft(TradeSearchDraft draft);

    Task<string?> LoadCategoryDisplayLabelAsync(
        TradeSearchDraft draft,
        CancellationToken cancellationToken = default);

    Task<PathOfExileTradePriceCheckResult> CheckAsync(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        CancellationToken cancellationToken = default);

    Task<PathOfExileTradePriceCheckResult> CheckAsync(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        int initialFetchResultCount,
        CancellationToken cancellationToken = default)
    {
        return CheckAsync(draft, validationResult, leagueIdentifier, cancellationToken);
    }

    Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
        string? searchQueryId,
        IReadOnlyList<string?>? resultIds,
        CancellationToken cancellationToken = default);
}

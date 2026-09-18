using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

/// <summary>
/// Invokes the same Core + App Trade pipeline used by Ctrl+D after clipboard text is acquired:
/// parse → base/modifier resolve → CreateDraft → provider component resolution.
/// Does not reimplement parser/resolver/mapper logic.
/// </summary>
internal static class ModifierPipelineReplayProductionPath
{
    public static ModifierPipelineReplayExecutionResult Execute(
        string rawClipboardText,
        GameDataCatalog gameDataCatalog,
        PathOfExileTradeStatCatalog tradeStatCatalog,
        PathOfExileTradeItemCatalog tradeItemCatalog,
        PathOfExileTradeFilterCatalog filterCatalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawClipboardText);
        ArgumentNullException.ThrowIfNull(gameDataCatalog);
        ArgumentNullException.ThrowIfNull(tradeStatCatalog);
        ArgumentNullException.ThrowIfNull(tradeItemCatalog);
        ArgumentNullException.ThrowIfNull(filterCatalog);

        // Same production types as PriceCheckerCapturedTextPreparation + TradeSearchDraftMapper
        // + PathOfExileTradePriceCheckService.ResolveProviderComponents.
        var parsed = new ItemTextParser().Parse(rawClipboardText);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, gameDataCatalog);
        var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
            parsed,
            gameDataCatalog,
            baseResolution);
        var draftResult = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            sourceResolutions,
            gameDataCatalog);
        if (!draftResult.IsSuccess || draftResult.Draft is null)
        {
            var diagnostic = draftResult.Diagnostics.FirstOrDefault();
            throw new InvalidOperationException(
                diagnostic is null
                    ? "Trade draft could not be created during replay."
                    : $"{diagnostic.Code}: {diagnostic.Message}");
        }

        var uniqueIdentity = new PathOfExileTradeItemIdentityMapper()
            .Map(draftResult.Draft, tradeItemCatalog)
            .Identity;
        var propertyDraft = new PathOfExileTradeItemPropertyResolver()
            .Resolve(draftResult.Draft, filterCatalog);
        var providerDraft = CreatePriceCheckService(tradeStatCatalog, tradeItemCatalog)
            .ResolveProviderComponents(
                propertyDraft,
                tradeStatCatalog,
                uniqueIdentity,
                filterCatalog);

        return new ModifierPipelineReplayExecutionResult(
            rawClipboardText,
            parsed,
            baseResolution,
            sourceResolutions,
            providerDraft,
            providerDraft.UniqueItemResolution);
    }

    private static PathOfExileTradePriceCheckService CreatePriceCheckService(
        PathOfExileTradeStatCatalog tradeStatCatalog,
        PathOfExileTradeItemCatalog tradeItemCatalog) =>
        new(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(tradeStatCatalog),
            new StaticItemProvider(tradeItemCatalog),
            new PathOfExileTradeSelectedModifierMapper(),
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient());

    private sealed class StaticStatProvider(PathOfExileTradeStatCatalog catalog) :
        IPathOfExileTradeStatCatalogProvider
    {
        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }

    private sealed class StaticItemProvider(PathOfExileTradeItemCatalog catalog) :
        IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeItemCatalogProviderResult.Success(catalog));
    }

    private sealed class NoSearchClient : IPathOfExileTradeSearchClient
    {
        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeSearchExecutionResult());
    }

    private sealed class NoFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeFetchExecutionResult());
    }
}

internal sealed record ModifierPipelineReplayExecutionResult(
    string RawClipboardText,
    ParsedItem Parsed,
    ItemBaseResolutionResult BaseResolution,
    IReadOnlyList<ModifierCandidateResolutionResult> SourceResolutions,
    TradeSearchDraft ProviderDraft,
    UniqueItemResolutionResult? UniqueResolution);

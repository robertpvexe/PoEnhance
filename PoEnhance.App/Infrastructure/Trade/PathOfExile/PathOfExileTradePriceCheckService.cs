using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradePriceCheckService : IPathOfExileTradePriceCheckService
{
    private readonly IPathOfExileTradeQueryBuilder queryBuilder;
    private readonly IPathOfExileTradeStatMatcher statMatcher;
    private readonly IPathOfExileTradeStatCatalogProvider statCatalogProvider;
    private readonly IPathOfExileTradeItemCatalogProvider itemCatalogProvider;
    private readonly IPathOfExileTradeSelectedModifierMapper selectedModifierMapper;
    private readonly IPathOfExileTradeItemIdentityMapper itemIdentityMapper;
    private readonly IPathOfExileTradeSearchClient searchClient;
    private readonly IPathOfExileTradeFetchClient fetchClient;
    private readonly IPathOfExileTradeFilterCatalogProvider? filterCatalogProvider;

    public PathOfExileTradePriceCheckService(
        IPathOfExileTradeQueryBuilder queryBuilder,
        IPathOfExileTradeStatMatcher statMatcher,
        IPathOfExileTradeStatCatalogProvider statCatalogProvider,
        IPathOfExileTradeItemCatalogProvider itemCatalogProvider,
        IPathOfExileTradeSelectedModifierMapper selectedModifierMapper,
        IPathOfExileTradeItemIdentityMapper itemIdentityMapper,
        IPathOfExileTradeSearchClient searchClient,
        IPathOfExileTradeFetchClient fetchClient,
        IPathOfExileTradeFilterCatalogProvider? filterCatalogProvider = null)
    {
        this.queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
        this.statMatcher = statMatcher ?? throw new ArgumentNullException(nameof(statMatcher));
        this.statCatalogProvider = statCatalogProvider ?? throw new ArgumentNullException(nameof(statCatalogProvider));
        this.itemCatalogProvider = itemCatalogProvider ?? throw new ArgumentNullException(nameof(itemCatalogProvider));
        this.selectedModifierMapper = selectedModifierMapper ?? throw new ArgumentNullException(nameof(selectedModifierMapper));
        this.itemIdentityMapper = itemIdentityMapper ?? throw new ArgumentNullException(nameof(itemIdentityMapper));
        this.searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        this.fetchClient = fetchClient ?? throw new ArgumentNullException(nameof(fetchClient));
        this.filterCatalogProvider = filterCatalogProvider;
    }

    public async Task<PathOfExileTradePriceCheckResult> CheckAsync(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        CancellationToken cancellationToken = default)
    {
        var effectiveDraft = draft;
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter> providerModifierFilters = [];
        PathOfExileTradeItemIdentity? providerItemIdentity = null;
        PathOfExileTradeFilterCatalog? providerFilterCatalog = null;
        PathOfExileTradeRateLimitSnapshot? catalogRateLimitSnapshot = null;
        IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> catalogDiagnostics = [];

        if (CanMapCategoryCriteria(draft, validationResult, leagueIdentifier) &&
            filterCatalogProvider is not null)
        {
            var filterCatalogResult = await filterCatalogProvider
                .GetCatalogAsync(cancellationToken)
                .ConfigureAwait(false);
            catalogRateLimitSnapshot = filterCatalogResult.RateLimitSnapshot;
            catalogDiagnostics = FilterCatalogSuccessDiagnostics(filterCatalogResult);
            if (!filterCatalogResult.IsSuccess || filterCatalogResult.Catalog is null)
            {
                return FilterCatalogFailure(filterCatalogResult);
            }

            providerFilterCatalog = filterCatalogResult.Catalog;
        }

        if (CanMapUniqueIdentity(draft, validationResult, leagueIdentifier))
        {
            var itemCatalogResult = await itemCatalogProvider
                .GetCatalogAsync(cancellationToken)
                .ConfigureAwait(false);
            catalogRateLimitSnapshot = itemCatalogResult.RateLimitSnapshot;
            catalogDiagnostics = ItemCatalogSuccessDiagnostics(itemCatalogResult);
            if (!itemCatalogResult.IsSuccess || itemCatalogResult.Catalog is null)
            {
                return ItemCatalogFailure(itemCatalogResult);
            }

            var identityResult = itemIdentityMapper.Map(draft, itemCatalogResult.Catalog);
            if (!identityResult.IsSuccess || identityResult.Identity is null)
            {
                return ItemIdentityFailure(
                    identityResult,
                    catalogRateLimitSnapshot,
                    catalogDiagnostics);
            }

            providerItemIdentity = identityResult.Identity;
        }

        if (CanMapSelectedModifiers(draft, validationResult, leagueIdentifier))
        {
            var catalogResult = await statCatalogProvider
                .GetCatalogAsync(cancellationToken)
                .ConfigureAwait(false);
            catalogRateLimitSnapshot = catalogResult.RateLimitSnapshot ?? catalogRateLimitSnapshot;
            catalogDiagnostics = catalogDiagnostics
                .Concat(CatalogSuccessDiagnostics(catalogResult))
                .ToArray();
            if (!catalogResult.IsSuccess || catalogResult.Catalog is null)
            {
                return CatalogFailure(catalogResult);
            }

            draft = ResolveProviderComponents(draft!, catalogResult.Catalog);
            effectiveDraft = draft;
            var mappingResult = selectedModifierMapper.Map(draft);
            if (!mappingResult.IsSuccess)
            {
                return MappingFailure(
                    mappingResult,
                    catalogRateLimitSnapshot,
                    catalogDiagnostics,
                    effectiveDraft);
            }

            providerModifierFilters = mappingResult.Filters;
        }

        var buildResult = queryBuilder.Build(
            draft,
            validationResult,
            leagueIdentifier,
            providerModifierFilters,
            providerItemIdentity,
            providerFilterCatalog);
        if (!buildResult.IsSuccess || buildResult.Request is null)
        {
            return new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.QueryBuild,
                EffectiveDraft = effectiveDraft,
                CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
                Diagnostics = catalogDiagnostics.Concat(MapQueryDiagnostics(
                        buildResult.Diagnostics,
                        PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed,
                        PathOfExileTradePriceCheckStage.QueryBuild,
                        "The Path of Exile Trade search request could not be built."))
                    .ToArray(),
            };
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CancelledBeforeHttp(
                PathOfExileTradePriceCheckStage.Search,
                catalogRateLimitSnapshot,
                catalogDiagnostics);
        }

        var searchResult = await searchClient.SearchAsync(
            buildResult.Request,
            buildResult.LeagueIdentifier ?? leagueIdentifier,
            cancellationToken);

        if (!searchResult.IsSuccess)
        {
            return new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Search,
                SearchQueryId = searchResult.Response?.Id,
                ProviderTotal = searchResult.Response?.Total,
                Inexact = searchResult.Response?.Inexact,
                EffectiveDraft = effectiveDraft,
                CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
                SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
                Diagnostics = catalogDiagnostics.Concat(SearchFailureDiagnostics(searchResult)).ToArray(),
                IsCancelled = searchResult.IsCancelled,
                IsTimeout = searchResult.IsTimeout,
            };
        }

        var searchResponse = searchResult.Response;
        var searchDiagnostics = MapHttpDiagnostics(
                searchResult.Diagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.SearchDiagnostic,
                PathOfExileTradePriceCheckStage.Search)
            .Concat(MapQueryDiagnostics(
                searchResult.RateLimitDiagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.SearchDiagnostic,
                PathOfExileTradePriceCheckStage.Search))
            .ToArray();

        if (string.IsNullOrWhiteSpace(searchResponse?.Id))
        {
            return new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Search,
                ProviderTotal = searchResponse?.Total,
                Inexact = searchResponse?.Inexact,
                EffectiveDraft = effectiveDraft,
                CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
                SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
                Diagnostics =
                [
                    .. catalogDiagnostics,
                    new PathOfExileTradePriceCheckDiagnostic(
                        PathOfExileTradePriceCheckDiagnosticCodes.MissingSearchQueryId,
                        "The Path of Exile Trade Search response did not include a query identifier.",
                        PathOfExileTradePriceCheckStage.Search),
                    .. searchDiagnostics,
                ],
            };
        }

        var searchQueryId = searchResponse.Id;
        var resultIds = searchResponse.Result ?? [];
        if (resultIds.Count == 0)
        {
            return new PathOfExileTradePriceCheckResult
            {
                IsSuccess = true,
                Stage = PathOfExileTradePriceCheckStage.Completed,
                SearchQueryId = searchQueryId,
                ProviderTotal = searchResponse.Total,
                Inexact = searchResponse.Inexact,
                EffectiveDraft = effectiveDraft,
                CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
                SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
                Diagnostics = catalogDiagnostics.Concat(searchDiagnostics).ToArray(),
            };
        }

        var fetchIds = resultIds
            .Take(PathOfExileTradeEndpointBuilder.MaximumFetchResultIds)
            .ToArray();

        if (cancellationToken.IsCancellationRequested)
        {
            return CancelledBeforeFetch(
                searchResult,
                searchQueryId,
                catalogRateLimitSnapshot,
                catalogDiagnostics,
                searchDiagnostics);
        }

        var fetchResult = await fetchClient.FetchAsync(searchQueryId, fetchIds, cancellationToken);
        if (!fetchResult.IsSuccess)
        {
            return new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Fetch,
                SearchQueryId = searchQueryId,
                ProviderTotal = searchResponse.Total,
                Inexact = searchResponse.Inexact,
                EffectiveDraft = effectiveDraft,
                CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
                SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
                FetchRateLimitSnapshot = fetchResult.RateLimitSnapshot,
                Diagnostics = catalogDiagnostics
                    .Concat(searchDiagnostics)
                    .Concat(FetchFailureDiagnostics(fetchResult))
                    .ToArray(),
                IsCancelled = fetchResult.IsCancelled,
                IsTimeout = fetchResult.IsTimeout,
            };
        }

        var fetchDiagnostics = MapHttpDiagnostics(
                fetchResult.Diagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.FetchDiagnostic,
                PathOfExileTradePriceCheckStage.Fetch)
            .Concat(MapQueryDiagnostics(
                fetchResult.RateLimitDiagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.FetchDiagnostic,
                PathOfExileTradePriceCheckStage.Fetch))
            .ToArray();

        return new PathOfExileTradePriceCheckResult
        {
            IsSuccess = true,
            Stage = PathOfExileTradePriceCheckStage.Completed,
            SearchQueryId = searchQueryId,
            ProviderTotal = searchResponse.Total,
            Inexact = searchResponse.Inexact,
            EffectiveDraft = effectiveDraft,
            Offers = fetchResult.Response?.Result ?? [],
            CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
            SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
            FetchRateLimitSnapshot = fetchResult.RateLimitSnapshot,
            Diagnostics = catalogDiagnostics.Concat(searchDiagnostics).Concat(fetchDiagnostics).ToArray(),
        };
    }

    private static bool CanMapSelectedModifiers(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier)
    {
        return draft?.ModifierFilters.Any(modifier => modifier.IsSelected) == true &&
            validationResult is not null &&
            !validationResult.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == TradeSearchValidationSeverity.Error) &&
            !string.IsNullOrWhiteSpace(leagueIdentifier);
    }

    internal TradeSearchDraft ResolveProviderComponents(
        TradeSearchDraft draft,
        PathOfExileTradeStatCatalog catalog)
    {
        if (draft.ModifierFilters.Count == 0)
        {
            return draft;
        }

        var resolvedComponents = draft.ModifierFilters
            .Select(component => ResolveProviderComponent(draft, component, catalog))
            .ToArray();
        var resolvedDraft = draft with
        {
            ModifierFilters = resolvedComponents,
        };

        return resolvedComponents.Any(component =>
                component.IsSelected &&
                component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.BaseGuaranteed &&
                CanUseAvailableExactBaseFallback(draft, component))
            ? ActivateAvailableExactBase(resolvedDraft)
            : resolvedDraft;
    }

    private ResolvedSearchComponent ResolveProviderComponent(
        TradeSearchDraft draft,
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog)
    {
        if (component.ProviderResolutionStatus != SearchComponentProviderResolutionStatus.NotResolved)
        {
            return component;
        }

        if (IsGuaranteedByActiveExactBase(draft, component))
        {
            return component with
            {
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.BaseGuaranteed,
            };
        }

        if (!CanResolveProviderComponent(component))
        {
            return component with
            {
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Unsupported,
                ProviderDiagnosticCode = PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
            };
        }

        var match = statMatcher.Match(
            component,
            catalog,
            CreateMatchContext(draft, component));
        if (component.IsSelected &&
            match.Status == PathOfExileTradeStatMatchStatus.NotFound &&
            CanUseAvailableExactBaseFallback(draft, component))
        {
            return component with
            {
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.BaseGuaranteed,
                ProviderCandidateStatIds = match.InitialCandidates
                    .Select(candidate => candidate.StatId)
                    .Where(statId => !string.IsNullOrWhiteSpace(statId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(statId => statId, StringComparer.Ordinal)
                    .ToArray(),
                ProviderDiagnosticCode = match.Diagnostics.FirstOrDefault()?.Code,
            };
        }

        var providerStatus = match.Status switch
        {
            PathOfExileTradeStatMatchStatus.Exact => SearchComponentProviderResolutionStatus.Exact,
            PathOfExileTradeStatMatchStatus.Ambiguous => SearchComponentProviderResolutionStatus.Ambiguous,
            PathOfExileTradeStatMatchStatus.NotFound => SearchComponentProviderResolutionStatus.NotFound,
            _ => SearchComponentProviderResolutionStatus.Unsupported,
        };
        var candidates = match.Candidates.Count > 0
            ? match.Candidates
            : match.InitialCandidates;

        return component with
        {
            ProviderResolutionStatus = providerStatus,
            ProviderStatId = match.ExactCandidate?.StatId,
            ProviderStatText = match.ExactCandidate?.Text,
            ProviderCandidateStatIds = candidates
                .Select(candidate => candidate.StatId)
                .Where(statId => !string.IsNullOrWhiteSpace(statId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(statId => statId, StringComparer.Ordinal)
                .ToArray(),
            ProviderDiagnosticCode = match.Diagnostics.FirstOrDefault()?.Code,
        };
    }

    private static bool CanResolveProviderComponent(ResolvedSearchComponent component)
    {
        if (component.ParsedKind == PoEnhance.Core.Items.Parsing.ParsedModifierKind.Implicit &&
            !string.IsNullOrWhiteSpace(component.CanonicalSignature) &&
            !string.IsNullOrWhiteSpace(component.OriginalText))
        {
            return true;
        }

        return component.IsSearchable &&
            component.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
            !string.IsNullOrWhiteSpace(component.ResolvedModifierId) &&
            component.ResolvedStatIds.Count > 0;
    }

    private static PathOfExileTradeStatMatchContext CreateMatchContext(
        TradeSearchDraft draft,
        ResolvedSearchComponent component)
    {
        return new PathOfExileTradeStatMatchContext
        {
            ItemClass = draft.ItemClass,
            ParsedBaseType = draft.ParsedBaseType,
            ModifierLocality = component.Locality,
            ResolvedModifierId = component.ResolvedModifierId,
            ResolvedModifierName = component.ResolvedModifierName,
            InternalStatIds = component.ResolvedStatIds,
        };
    }

    private static bool IsGuaranteedByActiveExactBase(
        TradeSearchDraft draft,
        ResolvedSearchComponent component)
    {
        if (!component.IsBaseImplicit ||
            draft.Base.ActiveCriterion?.Mode != BaseSearchMode.ExactBase)
        {
            return false;
        }

        var activeExactBase = TrimToNull(draft.Base.ActiveCriterion.ExactBaseName);
        var observedExactBase = TrimToNull(draft.Base.Observed?.ExactBaseName) ??
            TrimToNull(draft.Base.ResolvedBaseName);
        return activeExactBase is not null &&
            observedExactBase is not null &&
            string.Equals(activeExactBase, observedExactBase, StringComparison.Ordinal);
    }

    private static bool IsGuaranteedByAvailableExactBase(
        TradeSearchDraft draft,
        ResolvedSearchComponent component)
    {
        if (!component.IsBaseImplicit)
        {
            return false;
        }

        var availableExactBase = TrimToNull(draft.Base.AvailableCriteria.ExactBase?.ExactBaseName);
        var observedExactBase = TrimToNull(draft.Base.Observed?.ExactBaseName) ??
            TrimToNull(draft.Base.ResolvedBaseName);
        return availableExactBase is not null &&
            observedExactBase is not null &&
            string.Equals(availableExactBase, observedExactBase, StringComparison.Ordinal);
    }

    private static bool CanUseAvailableExactBaseFallback(
        TradeSearchDraft draft,
        ResolvedSearchComponent component)
    {
        return IsGuaranteedByAvailableExactBase(draft, component) &&
            IsKnownProviderlessDeterministicBaseIntrinsic(component);
    }

    private static bool IsKnownProviderlessDeterministicBaseIntrinsic(
        ResolvedSearchComponent component)
    {
        return component.ResolvedStatIds.Any(statId =>
                string.Equals(statId, "local_has_X_abyss_sockets", StringComparison.Ordinal)) ||
            string.Equals(
                component.CanonicalSignature?.Trim(),
                "Has # Abyssal Socket",
                StringComparison.Ordinal);
    }

    private static TradeSearchDraft ActivateAvailableExactBase(TradeSearchDraft draft)
    {
        var exactBase = draft.Base.AvailableCriteria.ExactBase;
        return exactBase is null
            ? draft
            : draft with
            {
                Base = draft.Base with
                {
                    ActiveCriterion = exactBase,
                },
            };
    }

    private static bool CanMapCategoryCriteria(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier)
    {
        return draft?.Base.ActiveCriterion?.Mode == BaseSearchMode.Category &&
            validationResult is not null &&
            !validationResult.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == TradeSearchValidationSeverity.Error) &&
            !string.IsNullOrWhiteSpace(leagueIdentifier);
    }

    private static bool CanMapUniqueIdentity(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier)
    {
        return draft?.Rarity?.Trim().Equals("Unique", StringComparison.OrdinalIgnoreCase) == true &&
            validationResult is not null &&
            !validationResult.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == TradeSearchValidationSeverity.Error) &&
            !string.IsNullOrWhiteSpace(leagueIdentifier);
    }

    private static PathOfExileTradePriceCheckResult CatalogFailure(
        PathOfExileTradeStatCatalogProviderResult result)
    {
        var code = result.IsCancelled
            ? PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadCancelled
            : result.IsTimeout
                ? PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadTimeout
                : PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed;

        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.CatalogLoad,
            CatalogRateLimitSnapshot = result.RateLimitSnapshot,
            Diagnostics = MapFailureDiagnostics(
                result.Diagnostics,
                result.RateLimitDiagnostics.Concat(result.ParserDiagnostics).ToArray(),
                code,
                PathOfExileTradePriceCheckStage.CatalogLoad,
                result.IsCancelled
                    ? "The Trade stats catalog load was cancelled."
                    : result.IsTimeout
                        ? "The Trade stats catalog load timed out."
                        : "The Trade stats catalog could not be loaded."),
            IsCancelled = result.IsCancelled,
            IsTimeout = result.IsTimeout,
        };
    }

    private static PathOfExileTradePriceCheckResult ItemCatalogFailure(
        PathOfExileTradeItemCatalogProviderResult result)
    {
        var code = result.IsCancelled
            ? PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadCancelled
            : result.IsTimeout
                ? PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadTimeout
                : PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed;

        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.CatalogLoad,
            CatalogRateLimitSnapshot = result.RateLimitSnapshot,
            Diagnostics = MapFailureDiagnostics(
                result.Diagnostics,
                result.RateLimitDiagnostics.Concat(result.ParserDiagnostics).ToArray(),
                code,
                PathOfExileTradePriceCheckStage.CatalogLoad,
                result.IsCancelled
                    ? "The Trade items catalog load was cancelled."
                    : result.IsTimeout
                        ? "The Trade items catalog load timed out."
                        : "The Trade items catalog could not be loaded."),
            IsCancelled = result.IsCancelled,
            IsTimeout = result.IsTimeout,
        };
    }

    private static PathOfExileTradePriceCheckResult FilterCatalogFailure(
        PathOfExileTradeFilterCatalogProviderResult result)
    {
        var code = result.IsCancelled
            ? PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadCancelled
            : result.IsTimeout
                ? PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadTimeout
                : PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed;

        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.CatalogLoad,
            CatalogRateLimitSnapshot = result.RateLimitSnapshot,
            Diagnostics = MapFailureDiagnostics(
                result.Diagnostics,
                result.RateLimitDiagnostics.Concat(result.ParserDiagnostics).ToArray(),
                code,
                PathOfExileTradePriceCheckStage.CatalogLoad,
                result.IsCancelled
                    ? "The Trade filters catalog load was cancelled."
                    : result.IsTimeout
                        ? "The Trade filters catalog load timed out."
                        : "The Trade filters catalog could not be loaded."),
            IsCancelled = result.IsCancelled,
            IsTimeout = result.IsTimeout,
        };
    }

    private static PathOfExileTradePriceCheckResult MappingFailure(
        PathOfExileTradeSelectedModifierMappingResult result,
        PathOfExileTradeRateLimitSnapshot? catalogRateLimitSnapshot,
        IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> catalogDiagnostics,
        TradeSearchDraft? effectiveDraft)
    {
        var mappingDiagnostics = result.Diagnostics
            .Select(diagnostic => new PathOfExileTradePriceCheckDiagnostic(
                PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed,
                diagnostic.Message,
                PathOfExileTradePriceCheckStage.ModifierMapping,
                diagnostic.Code))
            .ToArray();

        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.ModifierMapping,
            EffectiveDraft = effectiveDraft,
            CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
            Diagnostics = catalogDiagnostics
                .Concat(mappingDiagnostics.Length == 0
                    ?
                    [
                        new PathOfExileTradePriceCheckDiagnostic(
                            PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed,
                            "Selected modifiers could not be mapped to Trade filters.",
                            PathOfExileTradePriceCheckStage.ModifierMapping),
                    ]
                    : mappingDiagnostics)
                .ToArray(),
        };
    }

    private static PathOfExileTradePriceCheckResult ItemIdentityFailure(
        PathOfExileTradeItemIdentityMappingResult result,
        PathOfExileTradeRateLimitSnapshot? catalogRateLimitSnapshot,
        IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> catalogDiagnostics)
    {
        var diagnostics = result.Diagnostics
            .Select(diagnostic => new PathOfExileTradePriceCheckDiagnostic(
                PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed,
                diagnostic.Message,
                PathOfExileTradePriceCheckStage.QueryBuild,
                diagnostic.Code))
            .ToArray();

        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.QueryBuild,
            CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
            Diagnostics = catalogDiagnostics
                .Concat(diagnostics.Length == 0
                    ?
                    [
                        new PathOfExileTradePriceCheckDiagnostic(
                            PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed,
                            "The Unique item identity could not be mapped to Trade search.",
                            PathOfExileTradePriceCheckStage.QueryBuild),
                    ]
                    : diagnostics)
                .ToArray(),
        };
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> CatalogSuccessDiagnostics(
        PathOfExileTradeStatCatalogProviderResult result)
    {
        if (!result.IsSuccess)
        {
            return [];
        }

        return MapHttpDiagnostics(
                result.Diagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadDiagnostic,
                PathOfExileTradePriceCheckStage.CatalogLoad)
            .Concat(MapQueryDiagnostics(
                result.RateLimitDiagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadDiagnostic,
                PathOfExileTradePriceCheckStage.CatalogLoad))
            .ToArray();
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> ItemCatalogSuccessDiagnostics(
        PathOfExileTradeItemCatalogProviderResult result)
    {
        if (!result.IsSuccess)
        {
            return [];
        }

        return MapHttpDiagnostics(
                result.Diagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadDiagnostic,
                PathOfExileTradePriceCheckStage.CatalogLoad)
            .Concat(MapQueryDiagnostics(
                result.RateLimitDiagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadDiagnostic,
                PathOfExileTradePriceCheckStage.CatalogLoad))
            .ToArray();
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> FilterCatalogSuccessDiagnostics(
        PathOfExileTradeFilterCatalogProviderResult result)
    {
        if (!result.IsSuccess)
        {
            return [];
        }

        return MapHttpDiagnostics(
                result.Diagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadDiagnostic,
                PathOfExileTradePriceCheckStage.CatalogLoad)
            .Concat(MapQueryDiagnostics(
                result.RateLimitDiagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadDiagnostic,
                PathOfExileTradePriceCheckStage.CatalogLoad))
            .ToArray();
    }

    private static PathOfExileTradePriceCheckResult CancelledBeforeHttp(
        PathOfExileTradePriceCheckStage stage,
        PathOfExileTradeRateLimitSnapshot? catalogRateLimitSnapshot,
        IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> catalogDiagnostics)
    {
        return new PathOfExileTradePriceCheckResult
        {
            Stage = stage,
            CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
            Diagnostics =
            [
                .. catalogDiagnostics,
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled,
                    "The price check was cancelled before Search was requested.",
                    stage),
            ],
            IsCancelled = true,
        };
    }

    private static PathOfExileTradePriceCheckResult CancelledBeforeFetch(
        PathOfExileTradeSearchExecutionResult searchResult,
        string searchQueryId,
        PathOfExileTradeRateLimitSnapshot? catalogRateLimitSnapshot,
        IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> catalogDiagnostics,
        IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> searchDiagnostics)
    {
        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Fetch,
            SearchQueryId = searchQueryId,
            ProviderTotal = searchResult.Response?.Total,
            Inexact = searchResult.Response?.Inexact,
            CatalogRateLimitSnapshot = catalogRateLimitSnapshot,
            SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
            Diagnostics =
            [
                .. catalogDiagnostics,
                .. searchDiagnostics,
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled,
                    "The price check was cancelled before Fetch was requested.",
                    PathOfExileTradePriceCheckStage.Fetch),
            ],
            IsCancelled = true,
        };
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> SearchFailureDiagnostics(
        PathOfExileTradeSearchExecutionResult result)
    {
        var code = result.IsCancelled
            ? PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled
            : result.IsTimeout
                ? PathOfExileTradePriceCheckDiagnosticCodes.SearchTimeout
                : PathOfExileTradePriceCheckDiagnosticCodes.SearchFailed;
        return MapFailureDiagnostics(
            result.Diagnostics,
            result.RateLimitDiagnostics,
            code,
            PathOfExileTradePriceCheckStage.Search,
            result.IsCancelled
                ? "The Search request was cancelled."
                : result.IsTimeout
                    ? "The Search request timed out."
                    : "The Search request failed.");
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> FetchFailureDiagnostics(
        PathOfExileTradeFetchExecutionResult result)
    {
        var code = result.IsCancelled
            ? PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled
            : result.IsTimeout
                ? PathOfExileTradePriceCheckDiagnosticCodes.FetchTimeout
                : PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed;
        return MapFailureDiagnostics(
            result.Diagnostics,
            result.RateLimitDiagnostics,
            code,
            PathOfExileTradePriceCheckStage.Fetch,
            result.IsCancelled
                ? "The Fetch request was cancelled."
                : result.IsTimeout
                    ? "The Fetch request timed out."
                    : "The Fetch request failed.");
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> MapFailureDiagnostics(
        IReadOnlyList<PathOfExileTradeHttpDiagnostic> httpDiagnostics,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> queryDiagnostics,
        string code,
        PathOfExileTradePriceCheckStage stage,
        string fallbackMessage)
    {
        var diagnostics = MapHttpDiagnostics(httpDiagnostics, code, stage)
            .Concat(MapQueryDiagnostics(queryDiagnostics, code, stage))
            .ToArray();

        return diagnostics.Length == 0
            ? [new PathOfExileTradePriceCheckDiagnostic(code, fallbackMessage, stage)]
            : diagnostics;
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> MapHttpDiagnostics(
        IReadOnlyList<PathOfExileTradeHttpDiagnostic> diagnostics,
        string code,
        PathOfExileTradePriceCheckStage stage)
    {
        return diagnostics
            .Select(diagnostic => new PathOfExileTradePriceCheckDiagnostic(
                code,
                diagnostic.Message,
                stage,
                diagnostic.Code,
                diagnostic.HttpStatusCode,
                diagnostic.ProviderCode,
                diagnostic.ResultIndex))
            .ToArray();
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> MapQueryDiagnostics(
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics,
        string code,
        PathOfExileTradePriceCheckStage stage)
    {
        return diagnostics
            .Select(diagnostic => new PathOfExileTradePriceCheckDiagnostic(
                code,
                diagnostic.Message,
                stage,
                diagnostic.Code))
            .ToArray();
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> MapQueryDiagnostics(
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics,
        string code,
        PathOfExileTradePriceCheckStage stage,
        string fallbackMessage)
    {
        var mapped = MapQueryDiagnostics(diagnostics, code, stage);

        return mapped.Count == 0
            ? [new PathOfExileTradePriceCheckDiagnostic(code, fallbackMessage, stage)]
            : mapped;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

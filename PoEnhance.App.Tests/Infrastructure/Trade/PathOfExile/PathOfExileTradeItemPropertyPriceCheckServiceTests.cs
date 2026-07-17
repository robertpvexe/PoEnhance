using System.Text.Json;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemPropertyPriceCheckServiceTests
{
    private const string League = "Mirage";

    [Fact]
    public async Task Controller_PreparesItemPropertiesBeforeValidationWhenThereAreNoModifiers()
    {
        var filtersClient = SuccessFiltersClient();
        var service = CreateService(filtersClient);
        var controller = new PriceCheckerSearchController(service);
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.TotalDps,
                437.45m,
                selected: true)]);

        var prepared = await controller.PrepareDraftAsync(draft);
        var validation = new TradeSearchDraftValidator().Validate(prepared);

        Assert.Empty(prepared.ModifierFilters);
        var property = Assert.Single(prepared.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Exact, property.ProviderResolutionStatus);
        Assert.True(property.IsSearchable);
        Assert.True(validation.IsValid);
        Assert.Single(filtersClient.Calls);
    }

    [Fact]
    public async Task PropertyOnlySearch_ReusesCachedCatalogAndReachesSearchWithWeaponFilter()
    {
        var filtersClient = SuccessFiltersClient();
        var searchClient = new RecordingSearchClient();
        var service = CreateService(filtersClient, searchClient: searchClient);
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.TotalDps,
                437.45m,
                selected: true)]);
        var prepared = await service.PrepareEffectiveDraftAsync(draft);

        var result = await service.CheckAsync(
            prepared,
            new TradeSearchDraftValidator().Validate(prepared),
            League);

        Assert.True(
            result.IsSuccess,
            $"{result.Stage}: {string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}/{diagnostic.SourceCode}: {diagnostic.Message}"))}");
        Assert.Single(filtersClient.Calls);
        var call = Assert.Single(searchClient.Calls);
        using var json = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(call.Request!));
        Assert.Equal(437.45m, json.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("weapon_filters")
            .GetProperty("filters")
            .GetProperty("dps")
            .GetProperty("min")
            .GetDecimal());
    }

    [Fact]
    public async Task DirectCheck_RevalidatesAfterPropertyResolutionBeforeMappingSelectedModifier()
    {
        var filtersClient = SuccessFiltersClient();
        var searchClient = new RecordingSearchClient();
        var statCatalog = new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.stat_life",
                Text = "+# to maximum Life",
                Type = "explicit",
            },
        ]);
        var service = CreateService(
            filtersClient,
            statCatalog,
            searchClient,
            new StaticSelectedModifierMapper());
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.TotalDps,
                437.45m,
                selected: true)]) with
        {
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    OriginalText = "+55 to maximum Life",
                    CanonicalSignature = "+<number> to maximum Life",
                    ParsedKind = ParsedModifierKind.Prefix,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedModifierId = "mod.life",
                    ResolvedStatIds = ["base_maximum_life"],
                    IsSearchable = true,
                    SupportsValueBounds = true,
                    RequestedMinimum = 55m,
                    IsSelected = true,
                },
            ],
        };
        var initialValidation = new TradeSearchDraftValidator().Validate(draft);
        Assert.Contains(initialValidation.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedItemPropertyUnresolved);

        var result = await service.CheckAsync(draft, initialValidation, League);

        Assert.True(
            result.IsSuccess,
            $"{result.Stage}: {string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}/{diagnostic.SourceCode}: {diagnostic.Message}"))}");
        var request = Assert.Single(searchClient.Calls).Request!;
        using var json = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(request));
        var query = json.RootElement.GetProperty("query");
        Assert.Equal(437.45m, query
            .GetProperty("filters")
            .GetProperty("weapon_filters")
            .GetProperty("filters")
            .GetProperty("dps")
            .GetProperty("min")
            .GetDecimal());
        Assert.Equal("explicit.stat_life", query
            .GetProperty("stats")[0]
            .GetProperty("filters")[0]
            .GetProperty("id")
            .GetString());
    }

    [Fact]
    public async Task MissingReviewedCatalogEntryRejectsSelectedPropertyBeforeSearch()
    {
        var official = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
        var catalog = new PathOfExileTradeFilterCatalog(
            official.CategoryOptions,
            numericFilterDefinitions: official.NumericFilterDefinitions.Where(definition =>
                definition.FilterId != "dps"));
        var filtersClient = new RecordingFiltersClient(PathOfExileTradeFiltersExecutionResultSuccess(catalog));
        var searchClient = new RecordingSearchClient();
        var service = CreateService(filtersClient, searchClient: searchClient);
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.TotalDps,
                437.45m,
                selected: true)]);
        var prepared = await service.PrepareEffectiveDraftAsync(draft);

        var result = await service.CheckAsync(
            prepared,
            new TradeSearchDraftValidator().Validate(prepared),
            League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        Assert.Empty(searchClient.Calls);
        var property = Assert.Single(result.EffectiveDraft!.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Unresolved, property.ProviderResolutionStatus);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.SourceCode == PathOfExileTradeQueryDiagnosticCodes.LocallyInvalidDraft);
    }

    [Fact]
    public async Task FilterCatalogLoadFailureRejectsSelectedPropertyAndPreventsSearch()
    {
        var filtersClient = new RecordingFiltersClient(new PathOfExileTradeFiltersExecutionResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Catalog unavailable for test."),
            ],
        });
        var searchClient = new RecordingSearchClient();
        var service = CreateService(filtersClient, searchClient: searchClient);
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.TotalDps,
                437.45m,
                selected: true)]);

        var result = await service.CheckAsync(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, result.Stage);
        Assert.Empty(searchClient.Calls);
        Assert.Single(filtersClient.Calls);
        var property = Assert.Single(result.EffectiveDraft!.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Unresolved, property.ProviderResolutionStatus);
        Assert.Contains("could not be loaded", property.NotSearchableReason, StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed);
    }

    [Fact]
    public async Task UnselectedUnsupportedChaosDoesNotBlockModifierOnlySearch()
    {
        var filtersClient = SuccessFiltersClient();
        var searchClient = new RecordingSearchClient();
        var statCatalog = new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.stat_life",
                Text = "+# to maximum Life",
                Type = "explicit",
            },
        ]);
        var service = CreateService(
            filtersClient,
            statCatalog,
            searchClient,
            new StaticSelectedModifierMapper());
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.ChaosDps,
                42m)]) with
        {
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    OriginalText = "+55 to maximum Life",
                    CanonicalSignature = "+<number> to maximum Life",
                    ParsedKind = ParsedModifierKind.Prefix,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedModifierId = "mod.life",
                    ResolvedStatIds = ["base_maximum_life"],
                    IsSearchable = true,
                    ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                    ProviderStatId = "explicit.stat_life",
                    ProviderStatText = "+# to maximum Life",
                    SupportsValueBounds = true,
                    RequestedMinimum = 55m,
                    IsSelected = true,
                },
            ],
        };
        var prepared = await service.PrepareEffectiveDraftAsync(draft);

        var result = await service.CheckAsync(
            prepared,
            new TradeSearchDraftValidator().Validate(prepared),
            League);

        Assert.True(
            result.IsSuccess,
            $"{result.Stage}: {string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}/{diagnostic.SourceCode}: {diagnostic.Message}"))}");
        var chaos = Assert.Single(result.EffectiveDraft!.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Unsupported, chaos.ProviderResolutionStatus);
        Assert.False(chaos.IsSelected);
        var request = Assert.Single(searchClient.Calls).Request!;
        using var json = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(request));
        Assert.False(json.RootElement.GetProperty("query").GetProperty("filters")
            .TryGetProperty("weapon_filters", out _));
        Assert.Equal("explicit.stat_life", json.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")[0]
            .GetProperty("id")
            .GetString());
        Assert.Single(filtersClient.Calls);
    }

    private static PathOfExileTradePriceCheckService CreateService(
        RecordingFiltersClient filtersClient,
        PathOfExileTradeStatCatalog? statCatalog = null,
        RecordingSearchClient? searchClient = null,
        IPathOfExileTradeSelectedModifierMapper? selectedModifierMapper = null)
    {
        return new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatCatalogProvider(statCatalog ?? new PathOfExileTradeStatCatalog([])),
            new UnusedItemCatalogProvider(),
            selectedModifierMapper ?? new PathOfExileTradeSelectedModifierMapper(),
            new UnusedItemIdentityMapper(),
            searchClient ?? new RecordingSearchClient(),
            new UnusedFetchClient(),
            new PathOfExileTradeFilterCatalogProvider(filtersClient));
    }

    private static RecordingFiltersClient SuccessFiltersClient()
    {
        return new RecordingFiltersClient(PathOfExileTradeFiltersExecutionResultSuccess(
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog()));
    }

    private static PathOfExileTradeFiltersExecutionResult PathOfExileTradeFiltersExecutionResultSuccess(
        PathOfExileTradeFilterCatalog catalog)
    {
        return new PathOfExileTradeFiltersExecutionResult
        {
            IsSuccess = true,
            Catalog = catalog,
        };
    }

    private sealed class RecordingFiltersClient(PathOfExileTradeFiltersExecutionResult result)
        : IPathOfExileTradeFiltersClient
    {
        public List<CancellationToken> Calls { get; } = [];

        public Task<PathOfExileTradeFiltersExecutionResult> GetFiltersAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add(cancellationToken);
            return Task.FromResult(result);
        }
    }

    private sealed class StaticStatCatalogProvider(PathOfExileTradeStatCatalog catalog)
        : IPathOfExileTradeStatCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog cached)
        {
            cached = catalog;
            return true;
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
        }
    }

    private sealed class RecordingSearchClient : IPathOfExileTradeSearchClient
    {
        public List<SearchCall> Calls { get; } = [];

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new SearchCall(request, leagueIdentifier));
            return Task.FromResult(new PathOfExileTradeSearchExecutionResult
            {
                IsSuccess = true,
                Response = new PathOfExileTradeSearchResponse
                {
                    Id = "property-query",
                    Result = [],
                    Total = 0,
                },
            });
        }
    }

    private sealed class StaticSelectedModifierMapper : IPathOfExileTradeSelectedModifierMapper
    {
        public PathOfExileTradeSelectedModifierMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeStatCatalog? catalog = null)
        {
            return PathOfExileTradeSelectedModifierMappingResult.Success(
            [
                new PathOfExileTradeSelectedModifierFilter
                {
                    SourceIndex = 0,
                    SourceIndexes = [0],
                    StatId = "explicit.stat_life",
                    OriginalText = "+55 to maximum Life",
                    NormalizedItemTemplate = "+# to maximum Life",
                    ExtractedNumericValues = [55m],
                    Minimum = 55m,
                },
            ]);
        }
    }

    private sealed record SearchCall(
        PathOfExileTradeSearchRequest? Request,
        string? LeagueIdentifier);

    private sealed class UnusedItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Item catalog should not be used by these tests.");
    }

    private sealed class UnusedItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
    {
        public PathOfExileTradeItemIdentityMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeItemCatalog? catalog) =>
            throw new InvalidOperationException("Item identity should not be used by these tests.");
    }

    private sealed class UnusedFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Fetch should not be used for zero-result tests.");
    }
}

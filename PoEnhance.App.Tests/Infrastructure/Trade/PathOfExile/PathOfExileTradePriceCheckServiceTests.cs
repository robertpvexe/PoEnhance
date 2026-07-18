using System.Net;
using System.Reflection;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradePriceCheckServiceTests
{
    private const string League = "Mercenaries";

    [Fact]
    public void ResolveProviderComponents_OrdinaryUniqueScalar_UsesExplicitProviderOwnedProofAndCopiedBound()
    {
        var fixture = ServiceFixture.Create();
        var draft = UniqueDraft() with
        {
            ModifierFilters = [UniqueComponent("+69 to maximum Life", "+<number> to maximum Life") with
            {
                SupportsValueBounds = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                ObservedNumericValues = [69m],
                CanonicalNumericValues = [69m],
                RequestedMinimum = 69m,
            }],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.No));

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.True(
            component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"{component.ProviderResolutionStatus}: {component.ProviderDiagnosticCode} {component.ProviderDiagnosticMessage}");
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, component.StatMappingProof);
        Assert.Equal("explicit.stat_life", component.ProviderStatId);
        Assert.True(component.IsSearchable);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(69m, component.RequestedMinimum);
        Assert.False(component.IsSelected);
        Assert.DoesNotContain(component.FilterVariants, variant =>
            string.Equals(variant.ProviderKind, "pseudo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveProviderComponents_FoulbornPresence_RequiresFoulbornIdentityAndNeverBorrowsOrdinaryProof()
    {
        var fixture = ServiceFixture.Create();
        var component = UniqueComponent(
            "Test Foulborn presence",
            "Test Foulborn presence",
            ParsedUniqueModifierOrigin.Foulborn) with
        {
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
        };
        var draft = UniqueDraft("Foulborn Moonbender's Wing") with
        {
            ModifierFilters = [component],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.foulborn_presence", "Test Foulborn presence", "explicit"),
        ]);

        var ordinaryIdentity = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.No));
        var foulbornIdentity = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.Yes));

        var unsupported = Assert.Single(ordinaryIdentity.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, unsupported.ProviderResolutionStatus);
        Assert.False(unsupported.IsSearchable);
        Assert.False(unsupported.IsSelected);

        var exact = Assert.Single(foulbornIdentity.ModifierFilters);
        Assert.True(
            exact.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"{exact.ProviderResolutionStatus}: {exact.ProviderDiagnosticCode} {exact.ProviderDiagnosticMessage}");
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, exact.StatMappingProof);
        Assert.Equal("explicit.foulborn_presence", exact.ProviderStatId);
        Assert.False(exact.SupportsValueBounds);
        Assert.Null(exact.RequestedMinimum);
        Assert.Null(exact.RequestedMaximum);
        Assert.False(exact.IsSelected);
    }

    [Fact]
    public void ResolveProviderComponents_MultiLineUniqueBlockRejectsEveryLineInsteadOfPartialQuery()
    {
        var fixture = ServiceFixture.Create();
        var draft = UniqueDraft("Foulborn Midnight Bargain", "Calling Wand") with
        {
            ModifierFilters =
            [
                UniqueComponent("+1 to maximum number of Raised Zombies", "+<number> to maximum number of Raised Zombies") with
                {
                    SourceModifierIndex = 0,
                    SourceLineIndex = 0,
                },
                UniqueComponent("+1 to maximum number of Spectres", "+<number> to maximum number of Spectres") with
                {
                    SourceModifierIndex = 0,
                    SourceLineIndex = 1,
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.zombies", "+# to maximum number of Raised Zombies", "explicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.Yes));

        Assert.Equal(2, resolved.ModifierFilters.Count);
        Assert.All(resolved.ModifierFilters, component =>
        {
            Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
            Assert.Equal(
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.UniqueMultiLinePartialRepresentation,
                component.ProviderDiagnosticCode);
            Assert.False(component.IsSearchable);
            Assert.False(component.IsSelected);
            Assert.Null(component.ProviderStatId);
        });
    }

    [Fact]
    public void ResolveProviderComponents_UniqueWithOnlyBroadPseudoCandidateRemainsUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var draft = UniqueDraft() with
        {
            ModifierFilters = [UniqueComponent("+69 to maximum Life", "+<number> to maximum Life")],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.No));

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.NotFound, component.ProviderResolutionStatus);
        Assert.False(component.IsSearchable);
        Assert.False(component.IsSelected);
        Assert.Null(component.ProviderStatId);
    }

    [Fact]
    public async Task CheckAsync_ValidDraftBuildsSearchFetchesFirstBatchAndReturnsOrderedOffers()
    {
        var fixture = ServiceFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.SearchClient.Enqueue(SearchSuccess(ids, total: 12, inexact: true));
        fixture.FetchClient.Enqueue(FetchSuccess(ids.Take(10).Select(Offer).ToArray()));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Equal("query-1", result.SearchQueryId);
        Assert.Equal(ids, result.ResultIds);
        Assert.Equal(ids.Take(10), result.FetchedResultIds);
        Assert.Equal(12, result.ProviderTotal);
        Assert.True(result.Inexact);
        Assert.Equal(ids.Take(10), result.Offers.Select(offer => offer.Id));
        Assert.Empty(result.Diagnostics);
        Assert.Single(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.ItemCatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.ItemIdentityMapper.Calls);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Single(fixture.FetchClient.Calls);
        Assert.Equal(ids.Take(10), fixture.FetchClient.Calls[0].ResultIds);
        Assert.Equal("query-1", fixture.FetchClient.Calls[0].QueryId);
    }

    [Fact]
    public async Task CheckAsync_InitialFetchUsesOnlyTheMeasuredVisibleCapacity()
    {
        var fixture = ServiceFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.SearchClient.Enqueue(SearchSuccess(ids, total: 12));
        fixture.FetchClient.Enqueue(FetchSuccess(ids.Take(6).Select(Offer).ToArray()));

        var result = await fixture.Service.CheckAsync(
            Draft(),
            ValidationSuccess(),
            League,
            initialFetchResultCount: 6);

        Assert.True(result.IsSuccess);
        Assert.Equal(ids.Take(6), result.FetchedResultIds);
        Assert.Equal(ids.Take(6), result.Offers.Select(offer => offer.Id));
        Assert.Equal(ids.Take(6), Assert.Single(fixture.FetchClient.Calls).ResultIds);
    }

    [Fact]
    public async Task FetchMoreAsync_FetchesOnlyRequestedNextBatchWithoutRepeatingSearchAndReturnsProviderOrder()
    {
        var fixture = ServiceFixture.Create();
        var nextIds = Enumerable.Range(11, 10).Select(index => $"id-{index}").ToArray();
        fixture.FetchClient.Enqueue(FetchSuccess(nextIds.Reverse().Select(Offer).ToArray()));

        var result = await fixture.Service.FetchMoreAsync("query-1", nextIds);

        Assert.True(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Equal("query-1", result.SearchQueryId);
        Assert.Equal(nextIds, result.FetchedResultIds);
        Assert.Equal(nextIds, result.Offers.Select(offer => offer.Id));
        Assert.Empty(fixture.SearchClient.Calls);
        var fetch = Assert.Single(fixture.FetchClient.Calls);
        Assert.Equal("query-1", fetch.QueryId);
        Assert.Equal(nextIds, fetch.ResultIds);
    }

    [Fact]
    public async Task FetchMoreAsync_FetchFailureReturnsStructuredDiagnosticsWithoutRepeatingSearch()
    {
        var fixture = ServiceFixture.Create();
        fixture.FetchClient.Enqueue(new PathOfExileTradeFetchExecutionResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Fetch failed."),
            ],
        });

        var result = await fixture.Service.FetchMoreAsync("query-1", ["id-11"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Fetch, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Single(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_ZeroSearchResultsIsSuccessfulAndDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Equal("query-1", result.SearchQueryId);
        Assert.Equal(0, result.ProviderTotal);
        Assert.NotNull(result.Offers);
        Assert.Empty(result.Offers);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_QueryBuildFailureReturnsStructuredFailureAndSendsNoHttp()
    {
        var fixture = ServiceFixture.Create();
        fixture.QueryBuilder.Result = PathOfExileTradeQueryBuildResult.Failure(
            new PathOfExileTradeQueryDiagnostic("LOCAL_INVALID", "Local validation failed."));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed, diagnostic.Code);
        Assert.Equal("LOCAL_INVALID", diagnostic.SourceCode);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierLoadsCatalogMapsAndPassesProviderFiltersToQueryBuilder()
    {
        var fixture = ServiceFixture.Create();
        var catalog = Catalog();
        var providerFilters = new[] { ProviderFilter(0, "explicit.stat_life") };
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success(providerFilters);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.ItemCatalogProvider.Calls);
        var mappingCall = Assert.Single(fixture.SelectedModifierMapper.Calls);
        var resolvedComponent = Assert.Single(mappingCall.Draft!.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, resolvedComponent.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_life", resolvedComponent.ProviderStatId);
        Assert.Equal("+# to maximum Life", resolvedComponent.ProviderStatText);
        Assert.Same(providerFilters, Assert.Single(fixture.QueryBuilder.Calls).SelectedModifierFilters);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_ExactBaseSelectedBaseImplicitMarksComponentBaseGuaranteedBeforeMapping()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(ImplicitCatalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success([]);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            BaseImplicitDraft(BaseSearchMode.ExactBase),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var mappingCall = Assert.Single(fixture.SelectedModifierMapper.Calls);
        var component = Assert.Single(mappingCall.Draft!.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.BaseGuaranteed, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
    }

    [Fact]
    public async Task CheckAsync_CategorySelectedBaseImplicitResolvesProviderStatBeforeMapping()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(ImplicitCatalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success(
            [
                ProviderFilter(0, "implicit.stat_4082780964"),
            ]);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            BaseImplicitDraft(BaseSearchMode.Category),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var mappingCall = Assert.Single(fixture.SelectedModifierMapper.Calls);
        var component = Assert.Single(mappingCall.Draft!.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("implicit.stat_4082780964", component.ProviderStatId);
    }

    [Fact]
    public async Task CheckAsync_CategorySelectedBaseImplicitWithoutProviderStatActivatesExactBaseBeforeMapping()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(EmptyStatCatalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success([]);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            StygianViseBaseImplicitDraft(),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var mappingCall = Assert.Single(fixture.SelectedModifierMapper.Calls);
        Assert.Equal(BaseSearchMode.ExactBase, mappingCall.Draft!.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", mappingCall.Draft.Base.ActiveCriterion?.ExactBaseName);
        var component = Assert.Single(mappingCall.Draft.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.BaseGuaranteed, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        var queryCall = Assert.Single(fixture.QueryBuilder.Calls);
        Assert.Equal(BaseSearchMode.ExactBase, queryCall.Draft!.Base.ActiveCriterion?.Mode);
        Assert.Equal(BaseSearchMode.ExactBase, result.EffectiveDraft?.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", result.EffectiveDraft?.Base.ActiveCriterion?.ExactBaseName);
        Assert.Empty(queryCall.SelectedModifierFilters!);
    }

    [Fact]
    public void ResolveEffectiveDraft_SelectedDeterministicBaseImplicitActivatesAvailableExactBaseWithoutHttp()
    {
        var fixture = ServiceFixture.Create();

        var result = fixture.Service.ResolveEffectiveDraft(StygianViseBaseImplicitDraft());

        Assert.Equal(BaseSearchMode.ExactBase, result.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", result.Base.ActiveCriterion?.ExactBaseName);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_UniqueLoadsItemCatalogMapsIdentityAndPassesProviderIdentityToQueryBuilder()
    {
        var fixture = ServiceFixture.Create();
        var catalog = ItemCatalog();
        var identity = new PathOfExileTradeItemIdentity
        {
            CanonicalName = "Moonbender's Wing",
            CanonicalType = "Tomahawk",
            Foulborn = TradeTriState.No,
        };
        fixture.ItemCatalogProvider.Enqueue(PathOfExileTradeItemCatalogProviderResult.Success(catalog));
        fixture.ItemIdentityMapper.Result = PathOfExileTradeItemIdentityMappingResult.Success(identity);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var draft = UniqueDraft();
        var result = await fixture.Service.CheckAsync(draft, ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        var catalogCall = Assert.Single(fixture.ItemCatalogProvider.Calls);
        Assert.False(catalogCall.CancellationToken.IsCancellationRequested);
        var identityCall = Assert.Single(fixture.ItemIdentityMapper.Calls);
        Assert.Same(draft, identityCall.Draft);
        Assert.Same(catalog, identityCall.Catalog);
        Assert.Same(identity, Assert.Single(fixture.QueryBuilder.Calls).ProviderItemIdentity);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_UniqueItemCatalogFailurePreventsIdentityQueryBuildSearchAndFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.ItemCatalogProvider.Enqueue(new PathOfExileTradeItemCatalogProviderResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Items failed."),
            ],
        });

        var result = await fixture.Service.CheckAsync(UniqueDraft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed, diagnostic.Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure, diagnostic.SourceCode);
        Assert.Single(fixture.ItemCatalogProvider.Calls);
        Assert.Empty(fixture.ItemIdentityMapper.Calls);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_UniqueIdentityFailureReturnsQueryBuildFailureAndSendsNoSearch()
    {
        var fixture = ServiceFixture.Create();
        fixture.ItemIdentityMapper.Result =
            PathOfExileTradeItemIdentityMappingResult.Failure(
                new PathOfExileTradeItemIdentityMappingDiagnostic(
                    PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueDisplayVariant,
                    "Unsupported variant."));

        var result = await fixture.Service.CheckAsync(UniqueDraft("Foulborn Not Real"), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed, diagnostic.Code);
        Assert.Equal(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueDisplayVariant,
            diagnostic.SourceCode);
        Assert.Single(fixture.ItemCatalogProvider.Calls);
        Assert.Single(fixture.ItemIdentityMapper.Calls);
        Assert.Empty(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_UniqueSelectedModifierPassesItemIdentityAndProviderStatFilter()
    {
        var fixture = ServiceFixture.Create();
        var providerFilters = new[] { ProviderFilter(0, "explicit.stat_life") };
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(Catalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success(providerFilters);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            UniqueDraft() with
            {
                ModifierFilters = SelectedDraft().ModifierFilters,
            },
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.ItemCatalogProvider.Calls);
        Assert.Single(fixture.ItemIdentityMapper.Calls);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Single(fixture.SelectedModifierMapper.Calls);
        var queryCall = Assert.Single(fixture.QueryBuilder.Calls);
        Assert.NotNull(queryCall.ProviderItemIdentity);
        Assert.Same(providerFilters, queryCall.SelectedModifierFilters);
        Assert.Single(fixture.SearchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierWarningValidationStillLoadsCatalogAndMaps()
    {
        var fixture = ServiceFixture.Create();
        var providerFilters = new[] { ProviderFilter(0, "explicit.stat_life") };
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(Catalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success(providerFilters);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            SelectedDraft(),
            TradeSearchValidationResult.FromDiagnostics(
            [
                new TradeSearchValidationDiagnostic(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved,
                    TradeSearchValidationSeverity.Warning,
                    "Local modifier did not resolve.",
                    ModifierFilterIndex: 0),
            ]),
            League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Single(fixture.SelectedModifierMapper.Calls);
        Assert.Same(providerFilters, Assert.Single(fixture.QueryBuilder.Calls).SelectedModifierFilters);
        Assert.Single(fixture.SearchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_AdvancedRangeSelectedModifierMapsAndExecutesSearchAndFetch()
    {
        var queryBuilder = new FakeQueryBuilder
        {
            Result = PathOfExileTradeQueryBuildResult.Success(
                League,
                SearchRequest(),
                "{}",
                "Titan Plate",
                ItemBaseResolutionStatus.Exact),
        };
        var catalogProvider = new FakeCatalogProvider();
        catalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(Catalog()));
        var searchClient = new FakeSearchClient();
        searchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        var fetchClient = new FakeFetchClient();
        fetchClient.Enqueue(FetchSuccess([Offer("id-1")]));
        var service = new PathOfExileTradePriceCheckService(
            queryBuilder,
            new PathOfExileTradeStatMatcher(),
            catalogProvider,
            new FakeItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new FakeItemIdentityMapper(),
            searchClient,
            fetchClient);

        var result = await service.CheckAsync(
            SelectedDraft("+101(100-114) to maximum Life"),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var queryBuildCall = Assert.Single(queryBuilder.Calls);
        var providerFilter = Assert.Single(queryBuildCall.SelectedModifierFilters ?? []);
        Assert.Equal("explicit.stat_life", providerFilter.StatId);
        Assert.Equal("+# to maximum Life", providerFilter.NormalizedItemTemplate);
        Assert.Empty(providerFilter.ExtractedNumericValues);
        Assert.Single(searchClient.Calls);
        Assert.Single(fetchClient.Calls);
    }

    [Theory]
    [InlineData(0, "explicit.physical")]
    [InlineData(1, "explicit.physical")]
    [InlineData(2, "explicit.accuracy.local")]
    public async Task CheckAsync_EachDuplicateEffectComponentResolvesFromItsOwnProvenance(
        int selectedIndex,
        string expectedProviderStatId)
    {
        var queryBuilder = SuccessfulQueryBuilder();
        var catalogProvider = new FakeCatalogProvider();
        catalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(DuplicateEffectCatalog()));
        var searchClient = new FakeSearchClient();
        searchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        var fetchClient = new FakeFetchClient();
        fetchClient.Enqueue(FetchSuccess([Offer("id-1")]));
        var service = new PathOfExileTradePriceCheckService(
            queryBuilder,
            new PathOfExileTradeStatMatcher(),
            catalogProvider,
            new FakeItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new FakeItemIdentityMapper(),
            searchClient,
            fetchClient);

        var result = await service.CheckAsync(
            DuplicateEffectDraft(selectedIndex),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var effectiveDraft = Assert.IsType<TradeSearchDraft>(result.EffectiveDraft);
        Assert.Equal(3, effectiveDraft.ModifierFilters.Count);
        Assert.True(effectiveDraft.ModifierFilters[selectedIndex].IsSelected);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Exact,
            effectiveDraft.ModifierFilters[selectedIndex].ProviderResolutionStatus);
        Assert.Equal(expectedProviderStatId, effectiveDraft.ModifierFilters[selectedIndex].ProviderStatId);
        var providerFilter = Assert.Single(Assert.Single(queryBuilder.Calls).SelectedModifierFilters ?? []);
        Assert.Equal(expectedProviderStatId, providerFilter.StatId);
        Assert.Equal([selectedIndex], providerFilter.SourceIndexes);
    }

    [Fact]
    public async Task CheckAsync_TwoSelectedSourcesSharingPresenceStatStaySelectedAndSerializeOnce()
    {
        var queryBuilder = SuccessfulQueryBuilder();
        var catalogProvider = new FakeCatalogProvider();
        catalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(DuplicateEffectCatalog()));
        var searchClient = new FakeSearchClient();
        searchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        var fetchClient = new FakeFetchClient();
        fetchClient.Enqueue(FetchSuccess([Offer("id-1")]));
        var service = new PathOfExileTradePriceCheckService(
            queryBuilder,
            new PathOfExileTradeStatMatcher(),
            catalogProvider,
            new FakeItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new FakeItemIdentityMapper(),
            searchClient,
            fetchClient);

        var result = await service.CheckAsync(
            DuplicateEffectDraft(0, 1),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var effectiveDraft = Assert.IsType<TradeSearchDraft>(result.EffectiveDraft);
        Assert.True(effectiveDraft.ModifierFilters[0].IsSelected);
        Assert.True(effectiveDraft.ModifierFilters[1].IsSelected);
        Assert.NotEqual(
            effectiveDraft.ModifierFilters[0].ResolvedModifierId,
            effectiveDraft.ModifierFilters[1].ResolvedModifierId);
        var providerFilter = Assert.Single(Assert.Single(queryBuilder.Calls).SelectedModifierFilters ?? []);
        Assert.Equal("explicit.physical", providerFilter.StatId);
        Assert.Equal([0, 1], providerFilter.SourceIndexes);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierCatalogFailurePreventsMappingQueryBuildSearchAndFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(new PathOfExileTradeStatCatalogProviderResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Stats failed."),
            ],
        });

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed, Assert.Single(result.Diagnostics).Code);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierCatalogFailurePreservesUnderlyingDiagnostics()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(new PathOfExileTradeStatCatalogProviderResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge,
                    "Stats response exceeded the configured bound.",
                    HttpStatusCode.OK),
            ],
            ParserDiagnostics =
            [
                new PathOfExileTradeQueryDiagnostic(
                    PathOfExileTradeStatsDiagnosticCodes.MissingResultCollection,
                    "Missing result collection."),
            ],
        });

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, result.Stage);
        Assert.Equal(
            [
                PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge,
                PathOfExileTradeStatsDiagnosticCodes.MissingResultCollection,
            ],
            result.Diagnostics.Select(diagnostic => diagnostic.SourceCode));
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed, diagnostic.Code);
            Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, diagnostic.Stage);
        });
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierMappingFailurePreventsQueryBuildSearchAndFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(Catalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Failure(
            [
                new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous,
                    "Ambiguous modifier.",
                    SourceIndex: 0),
            ]);

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.ModifierMapping, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed, diagnostic.Code);
        Assert.Equal(PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous, diagnostic.SourceCode);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Single(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierLocalValidationFailureDoesNotLoadCatalog()
    {
        var fixture = ServiceFixture.Create();
        fixture.QueryBuilder.Result = PathOfExileTradeQueryBuildResult.Failure(
            new PathOfExileTradeQueryDiagnostic("LOCAL_INVALID", "Local validation failed."));

        var result = await fixture.Service.CheckAsync(
            SelectedDraft(),
            TradeSearchValidationResult.FromDiagnostics(
            [
                new TradeSearchValidationDiagnostic(
                    "LOCAL_INVALID",
                    TradeSearchValidationSeverity.Error,
                    "Local validation failed."),
            ]),
            League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Theory]
    [InlineData(false, false, PathOfExileTradePriceCheckDiagnosticCodes.SearchFailed)]
    [InlineData(true, false, PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled)]
    [InlineData(false, true, PathOfExileTradePriceCheckDiagnosticCodes.SearchTimeout)]
    public async Task CheckAsync_SearchFailureReturnsFailureAndDoesNotFetch(
        bool isCancelled,
        bool isTimeout,
        string expectedCode)
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(new PathOfExileTradeSearchExecutionResult
        {
            IsSuccess = false,
            IsCancelled = isCancelled,
            IsTimeout = isTimeout,
            HttpStatusCode = HttpStatusCode.BadGateway,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus,
                    "Search failed.",
                    HttpStatusCode.BadGateway),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(isCancelled, result.IsCancelled);
        Assert.Equal(isTimeout, result.IsTimeout);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus, result.Diagnostics[0].SourceCode);
        Assert.Equal(HttpStatusCode.BadGateway, result.Diagnostics[0].HttpStatusCode);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_MissingSearchQueryIdReturnsFailureAndDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], queryId: " "));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.MissingSearchQueryId, result.Diagnostics[0].Code);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Theory]
    [InlineData(false, false, PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed)]
    [InlineData(true, false, PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled)]
    [InlineData(false, true, PathOfExileTradePriceCheckDiagnosticCodes.FetchTimeout)]
    public async Task CheckAsync_FetchFailureReturnsFailureAfterOneFetch(
        bool isCancelled,
        bool isTimeout,
        string expectedCode)
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        fixture.FetchClient.Enqueue(new PathOfExileTradeFetchExecutionResult
        {
            IsSuccess = false,
            IsCancelled = isCancelled,
            IsTimeout = isTimeout,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Fetch failed."),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Fetch, result.Stage);
        Assert.Equal(isCancelled, result.IsCancelled);
        Assert.Equal(isTimeout, result.IsTimeout);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure, result.Diagnostics[0].SourceCode);
        Assert.Single(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_PreCancelledTokenBuildsNoSearchOrFetch()
    {
        var fixture = ServiceFixture.Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League, cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_CancelledBeforeFetchDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        using var cancellation = new CancellationTokenSource();
        fixture.SearchClient.AfterSearch = () => cancellation.Cancel();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League, cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(PathOfExileTradePriceCheckStage.Fetch, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_PreservesSeparateSearchAndFetchRateLimitSnapshots()
    {
        var fixture = ServiceFixture.Create();
        var searchRateLimit = RateLimit("trade-search");
        var fetchRateLimit = RateLimit("trade-fetch");
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1) with
        {
            RateLimitSnapshot = searchRateLimit,
        });
        fixture.FetchClient.Enqueue(FetchSuccess([Offer("id-1")]) with
        {
            RateLimitSnapshot = fetchRateLimit,
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Same(searchRateLimit, result.SearchRateLimitSnapshot);
        Assert.Same(fetchRateLimit, result.FetchRateLimitSnapshot);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierPreservesSeparateCatalogSearchAndFetchRateLimitSnapshots()
    {
        var fixture = ServiceFixture.Create();
        var catalogRateLimit = RateLimit("trade-stats");
        var searchRateLimit = RateLimit("trade-search");
        var fetchRateLimit = RateLimit("trade-fetch");
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(
            Catalog(),
            rateLimitSnapshot: catalogRateLimit));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success([ProviderFilter(0, "explicit.stat_life")]);
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1) with
        {
            RateLimitSnapshot = searchRateLimit,
        });
        fixture.FetchClient.Enqueue(FetchSuccess([Offer("id-1")]) with
        {
            RateLimitSnapshot = fetchRateLimit,
        });

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Same(catalogRateLimit, result.CatalogRateLimitSnapshot);
        Assert.Same(searchRateLimit, result.SearchRateLimitSnapshot);
        Assert.Same(fetchRateLimit, result.FetchRateLimitSnapshot);
    }

    [Fact]
    public async Task CheckAsync_PreservesPartialFetchDiagnosticsWhileRemainingSuccessful()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1", "bad"], total: 2));
        fixture.FetchClient.Enqueue(FetchSuccess([Offer("id-1")]) with
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.MalformedOffer,
                    "Offer could not be parsed.",
                    ResultIndex: 1),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.FetchDiagnostic, diagnostic.Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedOffer, diagnostic.SourceCode);
        Assert.Equal(1, diagnostic.ResultIndex);
    }

    [Fact]
    public async Task CheckAsync_DoesNotRetrySearchFetchOrRequestAdditionalBatches()
    {
        var fixture = ServiceFixture.Create();
        var ids = Enumerable.Range(1, 25).Select(index => $"id-{index}").ToArray();
        fixture.SearchClient.Enqueue(SearchSuccess(ids, total: 25));
        fixture.FetchClient.Enqueue(FetchSuccess(ids.Take(10).Select(Offer).ToArray()));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Single(fixture.FetchClient.Calls);
        var fetchedIds = Assert.IsAssignableFrom<IReadOnlyList<string?>>(fixture.FetchClient.Calls[0].ResultIds);
        Assert.Equal(10, fetchedIds.Count);
        Assert.Empty(fixture.SearchClient.PendingResults);
        Assert.Empty(fixture.FetchClient.PendingResults);
    }

    [Fact]
    public async Task CheckAsync_RevalidatesEffectiveDraftAndPassesLeagueToQueryBuilder()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft();
        var validation = ValidationSuccess();
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        await fixture.Service.CheckAsync(draft, validation, League);

        var call = Assert.Single(fixture.QueryBuilder.Calls);
        Assert.Same(draft, call.Draft);
        Assert.NotSame(validation, call.ValidationResult);
        Assert.NotNull(call.ValidationResult);
        Assert.True(call.ValidationResult!.IsValid);
        Assert.Equal(League, call.LeagueIdentifier);
    }

    [Fact]
    public void PriceCheckService_DoesNotConstructHttpClientOrDependOnUi()
    {
        var dependencyTypes = ReferencedMemberTypes(typeof(PathOfExileTradePriceCheckService)).ToArray();

        Assert.DoesNotContain(dependencyTypes, type => type == typeof(HttpClient));
        Assert.DoesNotContain(dependencyTypes, type => Contains(type, "PriceChecker"));
        Assert.DoesNotContain(dependencyTypes, type => Contains(type, "Wpf"));
    }

    [Fact]
    public void PriceCheckerWpfCodeBehind_DoesNotInvokeTradeServicesOrClients()
    {
        var wpfCodeBehindTypes = new[]
        {
            typeof(PriceCheckerWindow),
            typeof(PriceCheckerWindowFactory),
        };

        Assert.DoesNotContain(wpfCodeBehindTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradePriceCheckService") ||
            Contains(type, "PathOfExileTradeSearchClient") ||
            Contains(type, "PathOfExileTradeFetchClient"));
    }

    [Fact]
    public void CoreAssembly_GainsNoProviderSpecificDependency()
    {
        var coreAssembly = typeof(TradeSearchDraft).Assembly;

        Assert.DoesNotContain(coreAssembly.GetTypes(), type => Contains(type, "PathOfExileTrade"));
        Assert.DoesNotContain(coreAssembly.GetReferencedAssemblies(), assembly =>
            string.Equals(assembly.Name, "PoEnhance.App", StringComparison.Ordinal));
    }

    [Fact]
    public void PriceCheckService_DoesNotIntroduceCurrencyPublicStashCacheQueueSchedulerOrWaitTypes()
    {
        var providerTypes = typeof(PathOfExileTradePriceCheckService).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Infrastructure.Trade.PathOfExile")
            .Where(type => !type.IsNested && !type.Name.StartsWith("<", StringComparison.Ordinal))
            .Where(type => type.Name.Contains("PriceCheck", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.DoesNotContain(providerTypes, type =>
            Contains(type, "Currency") ||
            Contains(type, "PublicStash") ||
            Contains(type, "Cache") ||
            Contains(type, "Queue") ||
            Contains(type, "Scheduler") ||
            Contains(type, "Wait"));
        Assert.DoesNotContain(
            typeof(PathOfExileTradePriceCheckService).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("Retry", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Batch", StringComparison.OrdinalIgnoreCase));
    }

    private static TradeSearchDraft Draft()
    {
        return new TradeSearchDraft
        {
            ItemClass = "Body Armours",
            Rarity = "Rare",
            DisplayName = "Armoured Shell",
            ParsedBaseType = "Titan Plate",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.titan-plate",
                ResolvedBaseName = "Titan Plate",
            },
        };
    }

    private static TradeSearchDraft SelectedDraft(string originalText = "+55 to maximum Life")
    {
        return Draft() with
        {
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    OriginalText = originalText,
                    CanonicalSignature = "+# to maximum Life",
                    ParsedKind = PoEnhance.Core.Items.Parsing.ParsedModifierKind.Prefix,
                    Locality = ModifierLocality.Global,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedModifierId = "mod.life",
                    ResolvedStatIds = ["base_maximum_life"],
                    IsSearchable = true,
                    IsSelected = true,
                },
            ],
        };
    }

    private static TradeSearchDraft DuplicateEffectDraft(params int[] selectedIndexes)
    {
        var selected = selectedIndexes.ToHashSet();
        return Draft() with
        {
            ItemClass = "One Hand Axes",
            ParsedBaseType = "Test Weapon",
            ModifierFilters =
            [
                DuplicateEffectComponent(
                    "modifier:0:0",
                    sourceModifierIndex: 0,
                    sourceComponentIndex: 0,
                    "52% increased Physical Damage",
                    "<number>% increased Physical Damage",
                    "mod.pure-physical",
                    "local_physical_damage_percent",
                    selected.Contains(0)),
                DuplicateEffectComponent(
                    "modifier:1:0",
                    sourceModifierIndex: 1,
                    sourceComponentIndex: 0,
                    "39% increased Physical Damage",
                    "<number>% increased Physical Damage",
                    "mod.hybrid-physical-accuracy",
                    "local_physical_damage_percent",
                    selected.Contains(1)),
                DuplicateEffectComponent(
                    "modifier:1:1",
                    sourceModifierIndex: 1,
                    sourceComponentIndex: 1,
                    "+93 to Accuracy Rating",
                    "+<number> to Accuracy Rating",
                    "mod.hybrid-physical-accuracy",
                    "local_accuracy",
                    selected.Contains(2)),
            ],
        };
    }

    private static ResolvedSearchComponent DuplicateEffectComponent(
        string componentId,
        int sourceModifierIndex,
        int sourceComponentIndex,
        string originalText,
        string canonicalSignature,
        string modifierId,
        string statId,
        bool isSelected)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = componentId,
            SourceModifierIndex = sourceModifierIndex,
            SourceComponentIndex = sourceComponentIndex,
            OriginalText = originalText,
            CanonicalSignature = canonicalSignature,
            ParsedKind = PoEnhance.Core.Items.Parsing.ParsedModifierKind.Prefix,
            GenerationType = PoEnhance.GameData.ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Local,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = modifierId,
            ResolvedStatIds = [statId],
            IsSearchable = true,
            IsSelected = isSelected,
        };
    }

    private static TradeSearchDraft BaseImplicitDraft(BaseSearchMode activeMode)
    {
        var category = "Wand";
        var exactBaseName = "Blasting Wand";
        var categoryCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = category,
        };
        var exactBaseCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = category,
            ExactBaseName = exactBaseName,
        };

        return new TradeSearchDraft
        {
            ItemClass = "Wands",
            Rarity = "Rare",
            DisplayName = "Glyph Needle",
            ParsedBaseType = exactBaseName,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.blasting-wand",
                ResolvedBaseName = exactBaseName,
                Category = category,
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "base.blasting-wand",
                    ExactBaseName = exactBaseName,
                    Category = category,
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = categoryCriterion,
                    ExactBase = exactBaseCriterion,
                },
                ActiveCriterion = activeMode == BaseSearchMode.ExactBase
                    ? exactBaseCriterion
                    : categoryCriterion,
            },
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "base-implicit:0:mod.implicit.caster",
                    SourceModifierIndex = -1,
                    SourceComponentIndex = 0,
                    OriginalText = "Cannot roll Caster Modifiers",
                    CanonicalSignature = "Cannot roll Caster Modifiers",
                    ParsedKind = PoEnhance.Core.Items.Parsing.ParsedModifierKind.Implicit,
                    GenerationType = ModifierGenerationType.Implicit,
                    Locality = ModifierLocality.Global,
                    IsBaseImplicit = true,
                    GuaranteedExactBaseName = exactBaseName,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedModifierId = "mod.implicit.caster",
                    ResolvedStatIds = ["kinetic_wand_implicit_cannot_roll_caster_modifiers"],
                    IsSearchable = true,
                    IsSelected = true,
                },
            ],
        };
    }

    private static TradeSearchDraft StygianViseBaseImplicitDraft()
    {
        var draft = BaseImplicitDraft(BaseSearchMode.Category);
        var category = "Belt";
        var exactBaseName = "Stygian Vise";
        var categoryCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = category,
        };
        var exactBaseCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = category,
            ExactBaseName = exactBaseName,
        };

        return draft with
        {
            ItemClass = "Belts",
            DisplayName = "Corruption Bond",
            ParsedBaseType = exactBaseName,
            Base = draft.Base with
            {
                ResolvedBaseId = "base.stygian-vise",
                ResolvedBaseName = exactBaseName,
                Category = category,
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "base.stygian-vise",
                    ExactBaseName = exactBaseName,
                    Category = category,
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = categoryCriterion,
                    ExactBase = exactBaseCriterion,
                },
                ActiveCriterion = categoryCriterion,
            },
            ModifierFilters =
            [
                draft.ModifierFilters[0] with
                {
                    ComponentId = "base-implicit:0:StygianBeltImplicit1",
                    OriginalText = "Has 1 Abyssal Socket",
                    CanonicalSignature = "Has # Abyssal Socket",
                    GuaranteedExactBaseName = exactBaseName,
                    ResolvedModifierId = "StygianBeltImplicit1",
                    ResolvedStatIds = ["local_has_X_abyss_sockets"],
                },
            ],
        };
    }

    private static TradeSearchDraft UniqueDraft(
        string displayName = "Moonbender's Wing",
        string baseType = "Tomahawk")
    {
        return Draft() with
        {
            ItemClass = "One Hand Axes",
            Rarity = "Unique",
            DisplayName = displayName,
            ParsedBaseType = baseType,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.tomahawk",
                ResolvedBaseName = baseType,
            },
        };
    }

    private static ResolvedSearchComponent UniqueComponent(
        string originalText,
        string canonicalSignature,
        ParsedUniqueModifierOrigin origin = ParsedUniqueModifierOrigin.Ordinary)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = originalText,
            CanonicalSignature = canonicalSignature,
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = origin,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            IsSelected = false,
        };
    }

    private static PathOfExileTradeStatEntry Stat(string id, string text, string type)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = type,
            GroupLabel = type,
            Id = id,
            Text = text,
            Type = type,
        };
    }

    private static PathOfExileTradeItemIdentity UniqueIdentity(TradeTriState foulborn)
    {
        return new PathOfExileTradeItemIdentity
        {
            CanonicalName = "Moonbender's Wing",
            CanonicalType = "Tomahawk",
            Foulborn = foulborn,
        };
    }

    private static TradeSearchValidationResult ValidationSuccess()
    {
        return TradeSearchValidationResult.FromDiagnostics([]);
    }

    private static PathOfExileTradeSearchRequest SearchRequest()
    {
        return new PathOfExileTradeSearchRequest
        {
            Query = new PathOfExileTradeSearchQuery
            {
                Status = new PathOfExileTradeSearchStatus
                {
                    Option = "online",
                },
                Type = "Titan Plate",
            },
            Sort = new PathOfExileTradeSearchSort(),
        };
    }

    private static PathOfExileTradeSearchExecutionResult SearchSuccess(
        IReadOnlyList<string> ids,
        int total = 1,
        bool? inexact = null,
        string queryId = "query-1")
    {
        return new PathOfExileTradeSearchExecutionResult
        {
            IsSuccess = true,
            Response = new PathOfExileTradeSearchResponse
            {
                Id = queryId,
                Result = ids,
                Total = total,
                Inexact = inexact,
            },
        };
    }

    private static PathOfExileTradeFetchExecutionResult FetchSuccess(
        IReadOnlyList<PathOfExileTradeFetchedOffer> offers)
    {
        return new PathOfExileTradeFetchExecutionResult
        {
            IsSuccess = true,
            Response = new PathOfExileTradeFetchResponse
            {
                Result = offers,
            },
        };
    }

    private static PathOfExileTradeFetchedOffer Offer(string id)
    {
        return new PathOfExileTradeFetchedOffer
        {
            Id = id,
            Item = new PathOfExileTradeFetchedItem(),
            Listing = new PathOfExileTradeListing(),
        };
    }

    private static PathOfExileTradeRateLimitSnapshot RateLimit(string policy)
    {
        return new PathOfExileTradeRateLimitSnapshot
        {
            Policy = policy,
            Rules =
            [
                new PathOfExileTradeRateLimitRule
                {
                    RuleName = "Ip",
                    MaximumRequestCount = 30,
                    IntervalSeconds = 60,
                    TimeoutSeconds = 0,
                    CurrentRequestCount = 2,
                    CurrentTimeoutSeconds = 0,
                },
            ],
        };
    }

    private static PathOfExileTradeStatCatalog Catalog()
    {
        return new PathOfExileTradeStatCatalog(
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
    }

    private static PathOfExileTradeStatCatalog DuplicateEffectCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.physical",
                Text = "#% increased Physical Damage",
                Type = "explicit",
            },
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 1,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.accuracy.global",
                Text = "+# to Accuracy Rating",
                Type = "explicit",
            },
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 2,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.accuracy.local",
                Text = "+# to Accuracy Rating (Local)",
                Type = "explicit",
            },
        ]);
    }

    private static FakeQueryBuilder SuccessfulQueryBuilder()
    {
        return new FakeQueryBuilder
        {
            Result = PathOfExileTradeQueryBuildResult.Success(
                League,
                SearchRequest(),
                "{}",
                "Test Weapon",
                ItemBaseResolutionStatus.Exact),
        };
    }

    private static PathOfExileTradeStatCatalog ImplicitCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "implicit",
                GroupLabel = "Implicit",
                Id = "implicit.stat_4082780964",
                Text = "Cannot roll Caster Modifiers",
                Type = "implicit",
            },
        ]);
    }

    private static PathOfExileTradeStatCatalog EmptyStatCatalog()
    {
        return new PathOfExileTradeStatCatalog([]);
    }

    private static PathOfExileTradeItemCatalog ItemCatalog()
    {
        return new PathOfExileTradeItemCatalog(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "weapon",
                GroupLabel = "Weapons",
                Name = "Moonbender's Wing",
                Type = "Tomahawk",
                IsUnique = true,
            },
        ]);
    }

    private static PathOfExileTradeSelectedModifierFilter ProviderFilter(
        int sourceIndex,
        string statId)
    {
        return new PathOfExileTradeSelectedModifierFilter
        {
            SourceIndex = sourceIndex,
            StatId = statId,
            OriginalText = "+55 to maximum Life",
            NormalizedItemTemplate = "+# to maximum Life",
            ExtractedNumericValues = [55m],
        };
    }

    private static IEnumerable<Type> ReferencedMemberTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        return type.GetConstructors(flags).SelectMany(constructor =>
                constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
            .Concat(type.GetMethods(flags).SelectMany(method =>
                method.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed record QueryBuildCall(
        TradeSearchDraft? Draft,
        TradeSearchValidationResult? ValidationResult,
        string? LeagueIdentifier,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? SelectedModifierFilters,
        PathOfExileTradeItemIdentity? ProviderItemIdentity,
        PathOfExileTradeFilterCatalog? ProviderFilterCatalog,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? SelectedItemPropertyFilters);

    private sealed record CatalogCall(CancellationToken CancellationToken);

    private sealed record ItemIdentityMappingCall(
        TradeSearchDraft? Draft,
        PathOfExileTradeItemCatalog? Catalog);

    private sealed record MappingCall(TradeSearchDraft? Draft);

    private sealed record SearchCall(
        PathOfExileTradeSearchRequest? Request,
        string? LeagueIdentifier,
        CancellationToken CancellationToken);

    private sealed record FetchCall(
        string? QueryId,
        IReadOnlyList<string?>? ResultIds,
        CancellationToken CancellationToken);

    private sealed class ServiceFixture
    {
        private ServiceFixture(
            FakeQueryBuilder queryBuilder,
            FakeCatalogProvider catalogProvider,
            FakeItemCatalogProvider itemCatalogProvider,
            FakeSelectedModifierMapper selectedModifierMapper,
            FakeItemIdentityMapper itemIdentityMapper,
            FakeSearchClient searchClient,
            FakeFetchClient fetchClient)
        {
            QueryBuilder = queryBuilder;
            CatalogProvider = catalogProvider;
            ItemCatalogProvider = itemCatalogProvider;
            SelectedModifierMapper = selectedModifierMapper;
            ItemIdentityMapper = itemIdentityMapper;
            SearchClient = searchClient;
            FetchClient = fetchClient;
            Service = new PathOfExileTradePriceCheckService(
                queryBuilder,
                new PathOfExileTradeStatMatcher(),
                catalogProvider,
                itemCatalogProvider,
                selectedModifierMapper,
                itemIdentityMapper,
                searchClient,
                fetchClient);
        }

        public PathOfExileTradePriceCheckService Service { get; }

        public FakeQueryBuilder QueryBuilder { get; }

        public FakeCatalogProvider CatalogProvider { get; }

        public FakeItemCatalogProvider ItemCatalogProvider { get; }

        public FakeSelectedModifierMapper SelectedModifierMapper { get; }

        public FakeItemIdentityMapper ItemIdentityMapper { get; }

        public FakeSearchClient SearchClient { get; }

        public FakeFetchClient FetchClient { get; }

        public static ServiceFixture Create()
        {
            var queryBuilder = new FakeQueryBuilder
            {
                Result = PathOfExileTradeQueryBuildResult.Success(
                    League,
                    SearchRequest(),
                    "{}",
                    "Titan Plate",
                    ItemBaseResolutionStatus.Exact),
            };

            return new ServiceFixture(
                queryBuilder,
                new FakeCatalogProvider(),
                new FakeItemCatalogProvider(),
                new FakeSelectedModifierMapper(),
                new FakeItemIdentityMapper(),
                new FakeSearchClient(),
                new FakeFetchClient());
        }
    }

    private sealed class FakeQueryBuilder : IPathOfExileTradeQueryBuilder
    {
        public PathOfExileTradeQueryBuildResult Result { get; set; } =
            PathOfExileTradeQueryBuildResult.Failure();

        public List<QueryBuildCall> Calls { get; } = [];

        public PathOfExileTradeQueryBuildResult Build(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters = null,
            PathOfExileTradeItemIdentity? providerItemIdentity = null,
            PathOfExileTradeFilterCatalog? providerFilterCatalog = null,
            IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? selectedItemPropertyFilters = null)
        {
            Calls.Add(new QueryBuildCall(
                draft,
                validationResult,
                leagueIdentifier,
                selectedModifierFilters,
                providerItemIdentity,
                providerFilterCatalog,
                selectedItemPropertyFilters));
            return Result;
        }
    }

    private sealed class FakeCatalogProvider : IPathOfExileTradeStatCatalogProvider
    {
        public Queue<PathOfExileTradeStatCatalogProviderResult> PendingResults { get; } = [];

        public List<CatalogCall> Calls { get; } = [];

        public void Enqueue(PathOfExileTradeStatCatalogProviderResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new CatalogCall(cancellationToken));
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new PathOfExileTradeStatCatalogProviderResult
                {
                    IsCancelled = true,
                    Diagnostics =
                    [
                        new PathOfExileTradeHttpDiagnostic(
                            PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                            "Cancelled."),
                    ],
                });
            }

            return Task.FromResult(PendingResults.Count == 0
                ? PathOfExileTradeStatCatalogProviderResult.Success(Catalog())
                : PendingResults.Dequeue());
        }
    }

    private sealed class FakeItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
    {
        public Queue<PathOfExileTradeItemCatalogProviderResult> PendingResults { get; } = [];

        public List<CatalogCall> Calls { get; } = [];

        public void Enqueue(PathOfExileTradeItemCatalogProviderResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new CatalogCall(cancellationToken));
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new PathOfExileTradeItemCatalogProviderResult
                {
                    IsCancelled = true,
                    Diagnostics =
                    [
                        new PathOfExileTradeHttpDiagnostic(
                            PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                            "Cancelled."),
                    ],
                });
            }

            return Task.FromResult(PendingResults.Count == 0
                ? PathOfExileTradeItemCatalogProviderResult.Success(ItemCatalog())
                : PendingResults.Dequeue());
        }
    }

    private sealed class FakeSelectedModifierMapper : IPathOfExileTradeSelectedModifierMapper
    {
        public List<MappingCall> Calls { get; } = [];

        public PathOfExileTradeSelectedModifierMappingResult Result { get; set; } =
            PathOfExileTradeSelectedModifierMappingResult.Success([ProviderFilter(0, "explicit.stat_life")]);

        public PathOfExileTradeSelectedModifierMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeStatCatalog? catalog = null)
        {
            Calls.Add(new MappingCall(draft));
            return Result;
        }
    }

    private sealed class FakeItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
    {
        public List<ItemIdentityMappingCall> Calls { get; } = [];

        public PathOfExileTradeItemIdentityMappingResult Result { get; set; } =
            PathOfExileTradeItemIdentityMappingResult.Success(new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Moonbender's Wing",
                CanonicalType = "Tomahawk",
                Foulborn = TradeTriState.No,
            });

        public PathOfExileTradeItemIdentityMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeItemCatalog? catalog)
        {
            Calls.Add(new ItemIdentityMappingCall(draft, catalog));
            return Result;
        }
    }

    private sealed class FakeSearchClient : IPathOfExileTradeSearchClient
    {
        public Queue<PathOfExileTradeSearchExecutionResult> PendingResults { get; } = [];

        public List<SearchCall> Calls { get; } = [];

        public Action? AfterSearch { get; set; }

        public void Enqueue(PathOfExileTradeSearchExecutionResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new SearchCall(request, leagueIdentifier, cancellationToken));
            if (PendingResults.Count == 0)
            {
                throw new InvalidOperationException("No fake Search result was configured.");
            }

            var result = PendingResults.Dequeue();
            AfterSearch?.Invoke();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeFetchClient : IPathOfExileTradeFetchClient
    {
        public Queue<PathOfExileTradeFetchExecutionResult> PendingResults { get; } = [];

        public List<FetchCall> Calls { get; } = [];

        public void Enqueue(PathOfExileTradeFetchExecutionResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new FetchCall(queryId, resultIds, cancellationToken));
            if (PendingResults.Count == 0)
            {
                throw new InvalidOperationException("No fake Fetch result was configured.");
            }

            return Task.FromResult(PendingResults.Dequeue());
        }
    }
}

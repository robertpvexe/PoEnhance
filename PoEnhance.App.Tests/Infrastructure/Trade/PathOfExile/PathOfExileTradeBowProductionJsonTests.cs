using System.Text.Json;
using System.Text.RegularExpressions;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeBowProductionJsonTests
{
    private const string League = "Mirage";

    public static IEnumerable<object[]> CumulativeSelections()
    {
        yield return [Array.Empty<string>(), Array.Empty<string>(), Array.Empty<decimal>()];
        yield return [new[] { "Cold Damage" }, new[] { "explicit.stat_1037193709" }, new[] { 63.5m }];
        yield return
        [
            new[] { "Cold Damage", "Fire Damage" },
            new[] { "explicit.stat_1037193709", "explicit.stat_709508406" },
            new[] { 63.5m, 104.5m },
        ];
        yield return
        [
            new[] { "Cold Damage", "Fire Damage", "Lightning Damage" },
            new[] { "explicit.stat_1037193709", "explicit.stat_709508406", "explicit.stat_3336890334" },
            new[] { 63.5m, 104.5m, 82m },
        ];
        yield return
        [
            new[] { "Cold Damage", "Fire Damage", "Lightning Damage", "Dexterity" },
            new[]
            {
                "explicit.stat_1037193709",
                "explicit.stat_709508406",
                "explicit.stat_3336890334",
                "explicit.stat_3261801346",
            },
            new[] { 63.5m, 104.5m, 82m, 53m },
        ];
    }

    [Theory]
    [MemberData(nameof(CumulativeSelections))]
    public async Task SearchAsync_RangerBowCumulativeSelectionsReachFinalJsonWithProjectedBounds(
        IReadOnlyList<string> selectedRowFragments,
        IReadOnlyList<string> expectedProviderStatIds,
        IReadOnlyList<decimal> expectedMinimums)
    {
        var fixture = ProductionTradeFixture.Create();
        fixture.Controller.UpdateCurrentDraft(fixture.RangerBowDraft, fixture.RangerBowValidation);
        var elemental = Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ElementalDps);
        fixture.Window.RaiseItemPropertyExpansionChanged(elemental.SourceIndex, isExpanded: true);

        foreach (var selectedRowFragment in selectedRowFragments)
        {
            var row = Assert.Single(
                fixture.Window.CurrentSearchState!.Modifiers.Concat(
                    fixture.Window.CurrentSearchState.ItemProperties.SelectMany(property => property.Children)),
                modifier => modifier.Text.Contains(selectedRowFragment, StringComparison.Ordinal));
            fixture.Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: true);
        }

        var selectedCount = fixture.Window.CurrentSearchState!.SelectedModifierCount;

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.SearchClient.Calls);
        Assert.Equal(League, call.LeagueIdentifier);
        Assert.NotNull(call.Request);
        var json = PathOfExileTradeJson.SerializeSearchRequest(call.Request!);
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("type", out _));
        Assert.False(json.Contains("Ranger Bow", StringComparison.Ordinal));
        Assert.Equal("securable", query.GetProperty("status").GetProperty("option").GetString());
        Assert.Equal("asc", document.RootElement.GetProperty("sort").GetProperty("price").GetString());
        Assert.Equal("rare", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString());
        Assert.Equal("weapon.bow", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString());

        var statsGroup = Assert.Single(query.GetProperty("stats").EnumerateArray());
        Assert.Equal("and", statsGroup.GetProperty("type").GetString());
        var filters = statsGroup.GetProperty("filters").EnumerateArray().ToArray();
        Assert.Equal(selectedRowFragments.Count, selectedCount);
        Assert.Equal(selectedRowFragments.Count, filters.Length);
        var providerStatIds = filters
            .Select(filter => filter.GetProperty("id").GetString())
            .OfType<string>()
            .ToArray();
        Assert.Equal(expectedProviderStatIds, providerStatIds);
        Assert.Equal(filters.Length, providerStatIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(filters.Length, expectedMinimums.Count);
        for (var index = 0; index < filters.Length; index++)
        {
            var filter = filters[index];
            var value = filter.GetProperty("value");
            Assert.Equal(expectedMinimums[index], value.GetProperty("min").GetDecimal());
            Assert.Equal(JsonValueKind.Number, value.GetProperty("min").ValueKind);
            Assert.False(value.TryGetProperty("max", out _));
            Assert.False(filter.TryGetProperty("min", out _));
            Assert.False(filter.TryGetProperty("max", out _));
            Assert.False(filter.TryGetProperty("pseudo", out _));
            Assert.False(filter.TryGetProperty("disabled", out _));
            Assert.Equal(2, filter.EnumerateObject().Count());
        }
    }

    [Fact]
    public async Task SearchAsync_GolemFletchElementalParentAndThreeChildrenCoexistAcrossProviderBranches()
    {
        var fixture = ProductionTradeFixture.Create();
        var prepared = await fixture.Controller.PrepareDraftAsync(fixture.RangerBowDraft);
        var parent = Assert.Single(prepared.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ElementalDps);
        Assert.Equal(
            [
                "explicit.stat_1037193709",
                "explicit.stat_709508406",
                "explicit.stat_3336890334",
            ],
            prepared.ModifierFilters
                .Where(modifier => modifier.OriginalText.Contains(" Damage", StringComparison.Ordinal))
                .Select(modifier => modifier.ProviderStatId));
        Assert.All(
            prepared.ModifierFilters.Where(modifier =>
                modifier.OriginalText.Contains(" Damage", StringComparison.Ordinal)),
            modifier => Assert.DoesNotContain(
                modifier.FilterVariants,
                option => option.Label == "Pseudo"));
        Assert.False(parent.IsSelected);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Exact, parent.ProviderResolutionStatus);
        Assert.DoesNotContain(prepared.ModifierFilters, modifier => modifier.IsSelected);
        fixture.Controller.UpdateCurrentDraft(
            prepared,
            new TradeSearchDraftValidator().Validate(prepared));
        var propertyRow = Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ElementalDps);
        fixture.Window.RaiseItemPropertySelectionChanged(propertyRow.SourceIndex, isSelected: true);
        fixture.Window.RaiseItemPropertyExpansionChanged(propertyRow.SourceIndex, isExpanded: true);
        propertyRow = Assert.Single(fixture.Window.CurrentSearchState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ElementalDps);

        foreach (var fragment in new[] { "Cold Damage", "Fire Damage", "Lightning Damage" })
        {
            var row = Assert.Single(propertyRow.Children, modifier =>
                modifier.Text.Contains(fragment, StringComparison.Ordinal));
            fixture.Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: true);
        }

        await fixture.Controller.SearchAsync();

        var request = Assert.Single(fixture.SearchClient.Calls).Request!;
        var serialized = PathOfExileTradeJson.SerializeSearchRequest(request);
        using var document = JsonDocument.Parse(serialized);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal(325m, query
            .GetProperty("filters")
            .GetProperty("weapon_filters")
            .GetProperty("filters")
            .GetProperty("edps")
            .GetProperty("min")
            .GetDecimal());
        Assert.Equal("weapon.bow", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString());
        var stats = Assert.Single(query.GetProperty("stats").EnumerateArray());
        Assert.Equal("and", stats.GetProperty("type").GetString());
        Assert.Equal(
            [
                "explicit.stat_1037193709",
                "explicit.stat_709508406",
                "explicit.stat_3336890334",
            ],
            stats.GetProperty("filters")
                .EnumerateArray()
                .Select(filter => filter.GetProperty("id").GetString()));
        Assert.DoesNotContain("ItemPropertyContribution", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewedSemanticDescriptorId", serialized, StringComparison.Ordinal);
    }

    private sealed class ProductionTradeFixture
    {
        private ProductionTradeFixture(
            TradeSearchDraft rangerBowDraft,
            TradeSearchValidationResult rangerBowValidation,
            FakeWindow window,
            PriceCheckerSearchController controller,
            FakeSearchClient searchClient)
        {
            RangerBowDraft = rangerBowDraft;
            RangerBowValidation = rangerBowValidation;
            Window = window;
            Controller = controller;
            SearchClient = searchClient;
        }

        public TradeSearchDraft RangerBowDraft { get; }

        public TradeSearchValidationResult RangerBowValidation { get; }

        public FakeWindow Window { get; }

        public PriceCheckerSearchController Controller { get; }

        public FakeSearchClient SearchClient { get; }

        public static ProductionTradeFixture Create()
        {
            var catalog = LoadGameDataCatalog();
            var parsed = new ItemTextParser().Parse(CopiedItemCorpus.LoadItems()[2]);
            var displayService = new ParsedItemGameDataDisplayService();
            var baseResolution = displayService.ResolveItemBase(parsed, catalog).Result;
            Assert.NotNull(baseResolution);
            var modifierResolutions = displayService
                .ResolveModifierCandidates(parsed, catalog, baseResolution)
                .Results
                .Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();
            var draftResult = new TradeSearchDraftMapper().CreateDraft(
                parsed,
                baseResolution,
                modifierResolutions,
                catalog);
            Assert.True(draftResult.IsSuccess);
            Assert.NotNull(draftResult.Draft);
            Assert.Equal("Bow", draftResult.Draft!.Base.ActiveCriterion?.Category);
            Assert.Equal(BaseSearchMode.Category, draftResult.Draft.Base.ActiveCriterion?.Mode);
            Assert.Equal(
                ["Adds 46(41-55) to 81(81-95) Cold Damage",
                    "Adds 70(63-85) to 139(128-148) Fire Damage",
                    "Adds 9(8-10) to 155(148-173) Lightning Damage",
                    "+53(51-55) to Dexterity"],
                draftResult.Draft.ModifierFilters.Select(modifier => modifier.OriginalText).ToArray());

            var searchClient = new FakeSearchClient();
            var service = new PathOfExileTradePriceCheckService(
                new PathOfExileTradeQueryBuilder(),
                new PathOfExileTradeStatMatcher(),
                new FakeStatCatalogProvider(BowStatCatalog()),
                new FakeItemCatalogProvider(),
                new PathOfExileTradeSelectedModifierMapper(),
                new FakeItemIdentityMapper(),
                searchClient,
                new FakeFetchClient(),
                new FakeFilterCatalogProvider(BowFilterCatalog()));
            var window = new FakeWindow();
            var controller = new PriceCheckerSearchController(service);
            controller.AttachWindow(window);

            return new ProductionTradeFixture(
                draftResult.Draft,
                new TradeSearchDraftValidator().Validate(draftResult.Draft),
                window,
                controller,
                searchClient);
        }
    }

    private static PathOfExileTradeStatCatalog BowStatCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.stat_2387423236", "Adds # to # Cold Damage"),
            Entry(1, "explicit.stat_1037193709", "Adds # to # Cold Damage (Local)"),
            Entry(2, "explicit.stat_321077055", "Adds # to # Fire Damage"),
            Entry(3, "explicit.stat_709508406", "Adds # to # Fire Damage (Local)"),
            Entry(4, "explicit.stat_1334060246", "Adds # to # Lightning Damage"),
            Entry(5, "explicit.stat_3336890334", "Adds # to # Lightning Damage (Local)"),
            Entry(6, "explicit.stat_3261801346", "+# to Dexterity"),
            Entry(7, "pseudo.pseudo_adds_cold_damage", "Adds # to # Cold Damage", "pseudo"),
            Entry(8, "pseudo.pseudo_adds_fire_damage", "Adds # to # Fire Damage", "pseudo"),
            Entry(9, "pseudo.pseudo_adds_lightning_damage", "Adds # to # Lightning Damage", "pseudo"),
        ]);
    }

    private static PathOfExileTradeStatEntry Entry(
        int order,
        string id,
        string text,
        string kind = "explicit")
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = kind,
            GroupLabel = char.ToUpperInvariant(kind[0]) + kind[1..],
            Id = id,
            Text = text,
            Type = kind,
        };
    }

    private static PathOfExileTradeFilterCatalog BowFilterCatalog()
    {
        return PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    }

    private static GameDataCatalog LoadGameDataCatalog()
    {
        var packagePath = FindRepoFile("artifacts", "poenhance-game-data.json");
        var result = GameDataPackageLoader
            .LoadFromFileAsync(packagePath)
            .GetAwaiter()
            .GetResult();

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
        Assert.NotNull(result.Package);
        return GameDataCatalog.FromPackage(result.Package!);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file: {Path.Combine(relativeParts)}");
    }

    private static class CopiedItemCorpus
    {
        private static readonly Regex ItemBoundary = new(
            @"\r?\n\s*\r?\n(?=Item Class:)",
            RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> LoadItems()
        {
            var corpusPath = FindRepoFile("PoEnhance.Core.Tests", "TestData", "Items", "advanced-real-items-corpus.txt");
            var corpus = File.ReadAllText(corpusPath);
            var items = ItemBoundary
                .Split(corpus.TrimEnd('\r', '\n'))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

            Assert.Equal(15, items.Length);
            return items;
        }
    }

    private sealed record SearchCall(
        PathOfExileTradeSearchRequest? Request,
        string? LeagueIdentifier);

    private sealed class FakeStatCatalogProvider : IPathOfExileTradeStatCatalogProvider
    {
        private readonly PathOfExileTradeStatCatalog catalog;

        public FakeStatCatalogProvider(PathOfExileTradeStatCatalog catalog)
        {
            this.catalog = catalog;
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
        }
    }

    private sealed class FakeFilterCatalogProvider : IPathOfExileTradeFilterCatalogProvider
    {
        private readonly PathOfExileTradeFilterCatalog catalog;

        public FakeFilterCatalogProvider(PathOfExileTradeFilterCatalog catalog)
        {
            this.catalog = catalog;
        }

        public Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PathOfExileTradeFilterCatalogProviderResult.Success(catalog));
        }
    }

    private sealed class FakeSearchClient : IPathOfExileTradeSearchClient
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
                    Id = $"query-{Calls.Count}",
                    Result = [],
                    Total = 100 - Calls.Count,
                },
            });
        }
    }

    private sealed class FakeFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Fetch is not expected for zero-result fake Search responses.");
        }
    }

    private sealed class FakeItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Item catalog is not expected for ordinary Bow searches.");
        }
    }

    private sealed class FakeItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
    {
        public PathOfExileTradeItemIdentityMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeItemCatalog? catalog)
        {
            throw new InvalidOperationException("Item identity mapping is not expected for ordinary Bow searches.");
        }
    }

#pragma warning disable CS0067
    private sealed class FakeWindow : IPriceCheckerWindow
    {
        public event EventHandler? Closed;
        public event EventHandler? PanelActivated;
        public event EventHandler? PanelDeactivated;
        public event EventHandler? PanelInteraction;
        public event EventHandler? SearchRequested;

        public event EventHandler? LoadMoreRequested;

        public event EventHandler? TradeRequested;

        public event EventHandler<PriceCheckerOfferCapacityChangedEventArgs>? OfferCapacityChanged;
        public event EventHandler<PriceCheckerItemPropertySelectionChangedEventArgs>? ItemPropertySelectionChanged;
        public event EventHandler<PriceCheckerItemPropertyBoundsChangedEventArgs>? ItemPropertyBoundsChanged;
        public event EventHandler<PriceCheckerItemPropertyExpansionChangedEventArgs>? ItemPropertyExpansionChanged;
        public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;

        public event EventHandler<PriceCheckerModifierBoundsChangedEventArgs>? ModifierBoundsChanged;

        public event EventHandler<PriceCheckerModifierFilterVariantChangedEventArgs>? ModifierFilterVariantChanged;

        public event EventHandler<PriceCheckerModifierExpansionChangedEventArgs>? ModifierExpansionChanged;

        public event EventHandler? BaseCriterionToggleRequested;
        public event EventHandler<bool>? PinStateChanged;
        public event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;
        public event EventHandler? HorizontalDragCompleted;
        public event EventHandler? HorizontalResizeStarted;
        public event EventHandler<PriceCheckerHorizontalResizeEventArgs>? HorizontalResizeDelta;
        public event EventHandler? HorizontalResizeCompleted;
        public event EventHandler? ResetItemRequested;

        public bool IsClosed { get; private set; }

        public bool IsPinned { get; private set; }

        public PriceCheckerWindowState? CurrentState { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public PriceCheckerSearchViewState? CurrentSearchState { get; private set; }

        public PriceCheckerPlacement? GetDisplayedPlacement() => CurrentPlacement;

        public void UpdateContent(PriceCheckerWindowState state)
        {
            CurrentState = state;
        }

        public void UpdateSearch(PriceCheckerSearchViewState state)
        {
            CurrentSearchState = state;
        }

        public void ApplyPlacement(PriceCheckerPlacement placement)
        {
            CurrentPlacement = placement;
        }

        public void ShowInactive()
        {
        }

        public void Close()
        {
            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseModifierSelectionChanged(int modifierIndex, bool isSelected)
        {
            ModifierSelectionChanged?.Invoke(
                this,
                new PriceCheckerModifierSelectionChangedEventArgs(modifierIndex, isSelected));
        }

        public void RaiseItemPropertySelectionChanged(int propertyIndex, bool isSelected)
        {
            ItemPropertySelectionChanged?.Invoke(
                this,
                new PriceCheckerItemPropertySelectionChangedEventArgs(propertyIndex, isSelected));
        }

        public void RaiseItemPropertyExpansionChanged(int propertyIndex, bool isExpanded)
        {
            ItemPropertyExpansionChanged?.Invoke(
                this,
                new PriceCheckerItemPropertyExpansionChangedEventArgs(propertyIndex, isExpanded));
        }
    }
#pragma warning restore CS0067
}

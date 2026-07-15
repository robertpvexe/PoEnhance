using System.Text.Json;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeQueryBuilderCategoryProductionTests
{
    [Fact]
    public async Task PriceCheckerProductionPath_TradeCategoryMagicOneHandAxeZeroModifiersCreatesCategoryOnlyRequest()
    {
        var fixture = ProductionTradeCategoryFixture.Create(new PathOfExileTradeStatCatalog([]));
        fixture.Controller.UpdateCurrentDraft(fixture.MagicReaverAxeDraft, fixture.MagicReaverAxeValidation);
        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
        Assert.NotEqual("Select a supported Trade search.", fixture.Window.CurrentSearchState?.Message);
        var call = Assert.Single(fixture.SearchClient.Calls);
        var json = PathOfExileTradeJson.SerializeSearchRequest(call.Request!);
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");

        Assert.False(query.TryGetProperty("type", out _));
        Assert.False(query.TryGetProperty("name", out _));
        Assert.Equal("securable", query.GetProperty("status").GetProperty("option").GetString());
        Assert.Equal("magic", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString());
        Assert.Equal("weapon.oneaxe", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString());
        Assert.Empty(query
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.DoesNotContain("Reaver Axe", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Reaver Axe of Celebration", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PriceCheckerProductionPath_TradeCategoryMagicOneHandAxeSelectedAttackSpeedDoesNotFailAsUnsupportedCategory()
    {
        var fixture = ProductionTradeCategoryFixture.Create(AttackSpeedStatCatalog());
        fixture.Controller.UpdateCurrentDraft(fixture.MagicReaverAxeDraft, fixture.MagicReaverAxeValidation);
        var row = Assert.Single(fixture.Window.CurrentSearchState!.Modifiers);
        fixture.Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: true);

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
        Assert.NotEqual("Select a supported Trade search.", fixture.Window.CurrentSearchState?.Message);
        var call = Assert.Single(fixture.SearchClient.Calls);
        var json = PathOfExileTradeJson.SerializeSearchRequest(call.Request!);
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");

        Assert.False(query.TryGetProperty("type", out _));
        Assert.False(query.TryGetProperty("name", out _));
        Assert.Equal("securable", query.GetProperty("status").GetProperty("option").GetString());
        Assert.Equal("magic", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString());
        Assert.Equal("weapon.oneaxe", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString());
        var statsGroup = Assert.Single(query.GetProperty("stats").EnumerateArray());
        Assert.Equal("and", statsGroup.GetProperty("type").GetString());
        var statFilter = Assert.Single(statsGroup.GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_210067635", statFilter.GetProperty("id").GetString());
        Assert.False(statFilter.TryGetProperty("value", out _));
        Assert.False(statFilter.TryGetProperty("min", out _));
        Assert.False(statFilter.TryGetProperty("max", out _));
        Assert.Single(statFilter.EnumerateObject());
        Assert.Equal(
            fixture.Window.CurrentSearchState!.SelectedModifierCount,
            statsGroup.GetProperty("filters").EnumerateArray().Count());
    }

    [Fact]
    public void TradeCategoryCatalog_OneHandAxesResolvesProviderOptionIndependentOfCasingAndOrdering()
    {
        var catalog = new PathOfExileTradeFilterCatalog(
        [
            Category(0, "armour.shield", "Shield"),
            Category(1, "weapon.oneaxe", "One-Handed Axe"),
            Category(2, "weapon.bow", "Bow"),
        ]);

        Assert.True(catalog.TryFindCategoryOption("one hand axes", out var pluralOption));
        Assert.Equal("weapon.oneaxe", pluralOption.Id);

        Assert.True(catalog.TryFindCategoryOption("ONE HAND AXE", out var singularOption));
        Assert.Equal("weapon.oneaxe", singularOption.Id);
    }

    [Fact]
    public void TradeCategoryCatalog_ExistingOrdinaryCategoriesRemainProviderMapped()
    {
        var catalog = new PathOfExileTradeFilterCatalog(
        [
            Category(0, "weapon.bow", "Bow"),
            Category(1, "armour.shield", "Shield"),
            Category(2, "armour.chest", "Body Armour"),
            Category(3, "accessory.ring", "Ring"),
            Category(4, "weapon.wand", "Wand"),
            Category(5, "jewel.base", "Base Jewel"),
        ]);

        Assert.Equal("weapon.bow", Find(catalog, "Bow").Id);
        Assert.Equal("armour.shield", Find(catalog, "Shield").Id);
        Assert.Equal("armour.chest", Find(catalog, "Body Armour").Id);
        Assert.Equal("accessory.ring", Find(catalog, "Ring").Id);
        Assert.Equal("weapon.wand", Find(catalog, "Wand").Id);
        Assert.Equal("jewel.base", Find(catalog, "Jewel").Id);
    }

    [Theory]
    [InlineData("Wand", "Wand")]
    [InlineData("One Hand Axes", "One-Handed Axe")]
    [InlineData("Belt", "Belt")]
    public void TradeCategoryCatalog_DisplayLabelUsesOfficialProviderOptionText(
        string category,
        string expectedDisplayLabel)
    {
        var catalog = new PathOfExileTradeFilterCatalog(
        [
            Category(0, "weapon.wand", "Wand"),
            Category(1, "weapon.oneaxe", "One-Handed Axe"),
            Category(2, "accessory.belt", "Belt"),
        ]);

        Assert.True(catalog.TryGetCategoryDisplayLabel(category, out var displayLabel));
        Assert.Equal(expectedDisplayLabel, displayLabel);
    }

    [Fact]
    public void TradeCategoryCatalog_UnknownCategoryStillFailsExplicitly()
    {
        var catalog = new PathOfExileTradeFilterCatalog([Category(0, "weapon.bow", "Bow")]);

        Assert.False(catalog.TryFindCategoryOption("Unknown Category", out _));
    }

    private static PathOfExileTradeFilterOption Find(
        PathOfExileTradeFilterCatalog catalog,
        string category)
    {
        Assert.True(catalog.TryFindCategoryOption(category, out var option));
        return option;
    }

    private static PathOfExileTradeFilterOption Category(
        int order,
        string id,
        string text)
    {
        return new PathOfExileTradeFilterOption
        {
            ProviderOrder = order,
            GroupId = "type_filters",
            FilterId = "category",
            Id = id,
            Text = text,
        };
    }

    private sealed class ProductionTradeCategoryFixture
    {
        private ProductionTradeCategoryFixture(
            TradeSearchDraft magicReaverAxeDraft,
            TradeSearchValidationResult magicReaverAxeValidation,
            FakeWindow window,
            PriceCheckerSearchController controller,
            FakeSearchClient searchClient)
        {
            MagicReaverAxeDraft = magicReaverAxeDraft;
            MagicReaverAxeValidation = magicReaverAxeValidation;
            Window = window;
            Controller = controller;
            SearchClient = searchClient;
        }

        public TradeSearchDraft MagicReaverAxeDraft { get; }

        public TradeSearchValidationResult MagicReaverAxeValidation { get; }

        public FakeWindow Window { get; }

        public PriceCheckerSearchController Controller { get; }

        public FakeSearchClient SearchClient { get; }

        public static ProductionTradeCategoryFixture Create(PathOfExileTradeStatCatalog statCatalog)
        {
            var catalog = LoadGameDataCatalog();
            var parsed = new ItemTextParser().Parse(MagicReaverAxeText);
            var displayService = new ParsedItemGameDataDisplayService();
            var baseResolution = displayService.ResolveItemBase(parsed, catalog).Result;
            Assert.NotNull(baseResolution);
            Assert.Equal("Reaver Axe", baseResolution.ResolvedBaseName);

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
            Assert.Equal("Magic", draftResult.Draft!.Rarity);
            Assert.Equal("One Hand Axes", draftResult.Draft.Base.ActiveCriterion?.Category);
            Assert.Equal(BaseSearchMode.Category, draftResult.Draft.Base.ActiveCriterion?.Mode);
            Assert.Single(draftResult.Draft.ModifierFilters);

            var searchClient = new FakeSearchClient();
            var service = new PathOfExileTradePriceCheckService(
                new PathOfExileTradeQueryBuilder(),
                new PathOfExileTradeStatMatcher(),
                new FakeStatCatalogProvider(statCatalog),
                new FakeItemCatalogProvider(),
                new PathOfExileTradeSelectedModifierMapper(),
                new FakeItemIdentityMapper(),
                searchClient,
                new FakeFetchClient(),
                new FakeFilterCatalogProvider(OneHandAxeFilterCatalog()));
            var window = new FakeWindow();
            var controller = new PriceCheckerSearchController(service);
            controller.AttachWindow(window);

            return new ProductionTradeCategoryFixture(
                draftResult.Draft,
                new TradeSearchDraftValidator().Validate(draftResult.Draft),
                window,
                controller,
                searchClient);
        }
    }

    private static PathOfExileTradeFilterCatalog OneHandAxeFilterCatalog()
    {
        return new PathOfExileTradeFilterCatalog(
        [
            Category(0, "weapon.bow", "Bow"),
            Category(1, "weapon.oneaxe", "One-Handed Axe"),
            Category(2, "armour.shield", "Shield"),
        ]);
    }

    private static PathOfExileTradeStatCatalog AttackSpeedStatCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            Stat(0, "explicit.stat_681332047", "#% increased Attack Speed"),
            Stat(1, "explicit.stat_210067635", "#% increased Attack Speed (Local)"),
        ]);
    }

    private static PathOfExileTradeStatEntry Stat(
        int order,
        string id,
        string text)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = "explicit",
            GroupLabel = "Explicit",
            Id = id,
            Text = text,
            Type = "explicit",
        };
    }

    private const string MagicReaverAxeText = """
Item Class: One Hand Axes
Rarity: Magic
Reaver Axe of Celebration
--------
One Handed Axe
Physical Damage: 38-114
Critical Strike Chance: 5.00%
Attacks per Second: 1.51 (augmented)
Weapon Range: 1.1 metres
--------
Requirements:
Level: 61
Str: 167
Dex: 57
--------
Sockets: B B-R
--------
Item Level: 85
--------
{ Suffix Modifier "of Celebration" (Tier: 1) - Attack, Speed }
26(26-27)% increased Attack Speed
""";

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
                    Id = "query-1",
                    Result = [],
                    Total = 0,
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
            throw new InvalidOperationException("Item catalog is not expected for ordinary category searches.");
        }
    }

    private sealed class FakeItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
    {
        public PathOfExileTradeItemIdentityMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeItemCatalog? catalog)
        {
            throw new InvalidOperationException("Item identity mapping is not expected for ordinary category searches.");
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
        public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;

        public event EventHandler? BaseCriterionToggleRequested;
        public event EventHandler<bool>? PinStateChanged;
        public event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;
        public event EventHandler? HorizontalDragCompleted;
        public event EventHandler? HorizontalResizeStarted;
        public event EventHandler<PriceCheckerHorizontalResizeEventArgs>? HorizontalResizeDelta;
        public event EventHandler? HorizontalResizeCompleted;
        public event EventHandler? ResetPositionRequested;

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
    }
#pragma warning restore CS0067
}

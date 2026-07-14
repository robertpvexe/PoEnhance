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

public sealed class PathOfExileTradeImplicitProductionTests
{
    private const string League = "Mercenaries";

    [Fact]
    public async Task BlastingWandImplicitSelectedCreatesOneImplicitProviderFilter()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "explicit.caster", "Cannot roll Caster Modifiers", "explicit"),
            Stat(1, "implicit.caster", "Cannot roll Caster Modifiers", "implicit")));
        fixture.OpenText(CorpusItem(4));
        fixture.SelectRow("Cannot roll Caster Modifiers");

        await fixture.SearchAsync();

        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("rare", Rarity(query));
        Assert.Equal("weapon.wand", Category(query));
        Assert.Equal("securable", query.GetProperty("status").GetProperty("option").GetString());
        Assert.False(query.TryGetProperty("type", out _));
        Assert.Equal(["implicit.caster"], StatIds(query));
        Assert.Equal(fixture.Window.CurrentSearchState!.SelectedModifierCount, StatIds(query).Length);
    }

    [Fact]
    public async Task SupremeSpikedShieldImplicitSelectedCreatesOneImplicitProviderFilterAndKeepsFourRows()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "explicit.suppress", "+#% chance to Suppress Spell Damage", "explicit"),
            Stat(1, "implicit.suppress", "+#% chance to Suppress Spell Damage", "implicit")));
        fixture.OpenText(SupremeSpikedShieldText);
        fixture.SelectRow("chance to Suppress Spell Damage");

        await fixture.SearchAsync();

        Assert.Equal(4, fixture.Window.CurrentSearchState!.Modifiers.Count);
        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("magic", Rarity(query));
        Assert.Equal("armour.shield", Category(query));
        Assert.Equal(["implicit.suppress"], StatIds(query));
    }

    [Fact]
    public async Task OrganicRingEachImplicitSelectedAloneCreatesOneImplicitProviderFilter()
    {
        var catalog = OrganicCatalog();
        foreach (var testCase in new[]
        {
            ("additional Physical Damage Reduction", "implicit.phys_reduction"),
            ("Cannot roll Modifiers of Non-Physical Damage Types", "implicit.non_phys"),
        })
        {
            var fixture = Fixture.Create(catalog);
            fixture.OpenText(CorpusItem(7));
            fixture.SelectRow(testCase.Item1);

            await fixture.SearchAsync();

            var json = fixture.SingleSearchJson();
            using var document = JsonDocument.Parse(json);
            var query = document.RootElement.GetProperty("query");
            Assert.Equal("rare", Rarity(query));
            Assert.Equal("accessory.ring", Category(query));
            Assert.Equal([testCase.Item2], StatIds(query));
        }
    }

    [Fact]
    public async Task OrganicRingBothImplicitsSelectedCreateTwoDistinctAndFilters()
    {
        var fixture = Fixture.Create(OrganicCatalog());
        fixture.OpenText(CorpusItem(7));
        fixture.SelectRow("additional Physical Damage Reduction");
        fixture.SelectRow("Cannot roll Modifiers of Non-Physical Damage Types");

        await fixture.SearchAsync();

        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        var statsGroup = Assert.Single(query.GetProperty("stats").EnumerateArray());
        Assert.Equal("and", statsGroup.GetProperty("type").GetString());
        var ids = StatIds(query);
        Assert.Equal(["implicit.phys_reduction", "implicit.non_phys"], ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(fixture.Window.CurrentSearchState!.SelectedModifierCount, ids.Length);
    }

    [Fact]
    public async Task StygianViseSelectedAbyssalSocketUsesExactBaseFallbackWithoutStatFilter()
    {
        var fixture = Fixture.Create(Catalog());
        fixture.OpenText(CorpusItem(10));
        fixture.SelectRow("Has 1 Abyssal Socket");

        await fixture.SearchAsync();

        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("rare", Rarity(query));
        Assert.Equal("Stygian Vise", query.GetProperty("type").GetString());
        Assert.False(query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .TryGetProperty("category", out _));
        Assert.Empty(StatIds(query));

        var state = Assert.IsType<PriceCheckerWindowState>(fixture.Window.CurrentState);
        Assert.Equal(BaseSearchMode.ExactBase, state.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", state.Draft.Base.ActiveCriterion?.ExactBaseName);
        Assert.Equal(
            "Exact; Search: Exact Base: Stygian Vise",
            PriceCheckerWindow.FormatBaseStatus(state.Draft.Base));
        var component = Assert.Single(state.Draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Has 1 Abyssal Socket", StringComparison.Ordinal));
        Assert.True(component.IsSelected);
        Assert.Equal(SearchComponentProviderResolutionStatus.BaseGuaranteed, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.DoesNotContain(state.ValidationResult.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved);
        Assert.Contains(state.ValidationResult.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierRepresentedByExactBase &&
            diagnostic.Severity == TradeSearchValidationSeverity.Info);
    }

    [Fact]
    public async Task StygianViseUnselectedAbyssalSocketRemainsCategoryBeltSearch()
    {
        var fixture = Fixture.Create(Catalog());
        var draft = fixture.OpenText(CorpusItem(10));

        await fixture.SearchAsync();

        Assert.Equal(BaseSearchMode.Category, draft.Base.ActiveCriterion?.Mode);
        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("type", out _));
        Assert.Equal("accessory.belt", Category(query));
        Assert.Empty(StatIds(query));
        var state = Assert.IsType<PriceCheckerWindowState>(fixture.Window.CurrentState);
        Assert.Equal(BaseSearchMode.Category, state.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(
            "Exact; Search: Category: Belt",
            PriceCheckerWindow.FormatBaseStatus(state.Draft.Base));
    }

    [Fact]
    public void ParsedOrdinaryImplicitsCarryBaseImplicitProvenanceWithoutReminderOrUnscalableText()
    {
        var fixture = Fixture.Create(OrganicCatalog());
        var draft = fixture.OpenText(CorpusItem(7));

        var physical = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("additional Physical Damage Reduction", StringComparison.Ordinal));
        var cannotRoll = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Cannot roll Modifiers of Non-Physical Damage Types", StringComparison.Ordinal));

        Assert.Equal(ParsedModifierKind.Implicit, physical.ParsedKind);
        Assert.Equal(ParsedModifierKind.Implicit, cannotRoll.ParsedKind);
        Assert.Equal("UulNetolBreachRingImplicit", physical.ResolvedModifierId);
        Assert.Equal("UulNetolBreachRingImplicit", cannotRoll.ResolvedModifierId);
        Assert.Equal(["base_additional_physical_damage_reduction_%"], physical.ResolvedStatIds);
        Assert.Equal(["breach_ring_uulnetol_implicit"], cannotRoll.ResolvedStatIds);
        Assert.DoesNotContain("Unscalable Value", cannotRoll.OriginalText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingImplicitProviderCandidateRemainsExplicitlyUnresolved()
    {
        var fixture = Fixture.Create(Catalog());
        fixture.OpenText(CorpusItem(4));
        fixture.SelectRow("Cannot roll Caster Modifiers");

        await fixture.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState!.Status);
        Assert.Equal("Selected modifier is not available in Trade search.", fixture.Window.CurrentSearchState.Message);
        Assert.Empty(fixture.SearchClient.Calls);
    }

    private static PathOfExileTradeStatCatalog OrganicCatalog()
    {
        return Catalog(
            Stat(0, "implicit.phys_reduction", "#% additional Physical Damage Reduction", "implicit"),
            Stat(1, "implicit.non_phys", "Cannot roll Modifiers of Non-Physical Damage Types", "implicit"));
    }

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] stats)
    {
        return new PathOfExileTradeStatCatalog(stats);
    }

    private static PathOfExileTradeStatEntry Stat(
        int order,
        string id,
        string text,
        string group)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = group,
            GroupLabel = group,
            Id = id,
            Text = text,
            Type = group,
        };
    }

    private static string Rarity(JsonElement query)
    {
        return query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString()!;
    }

    private static string Category(JsonElement query)
    {
        return query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString()!;
    }

    private static string[] StatIds(JsonElement query)
    {
        return query
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .Select(filter =>
            {
                Assert.False(filter.TryGetProperty("value", out _));
                Assert.False(filter.TryGetProperty("min", out _));
                Assert.False(filter.TryGetProperty("max", out _));
                Assert.Single(filter.EnumerateObject());
                return filter.GetProperty("id").GetString()!;
            })
            .ToArray();
    }

    private static string CorpusItem(int index)
    {
        var corpusPath = FindRepoFile("PoEnhance.Core.Tests", "TestData", "Items", "advanced-real-items-corpus.txt");
        var corpus = File.ReadAllText(corpusPath);
        var items = new Regex(@"\r?\n\s*\r?\n(?=Item Class:)", RegexOptions.CultureInvariant)
            .Split(corpus.TrimEnd('\r', '\n'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        return items[index];
    }

    private const string SupremeSpikedShieldText = """
Item Class: Shields
Rarity: Magic
Wasp's Supreme Spiked Shield of Thick Skin
--------
Chance to Block: 24%
Evasion Rating: 362 (augmented)
Energy Shield: 73 (augmented)
--------
Requirements:
Level: 70
Dex: 85
Int: 85
--------
Sockets: B-B-G
--------
Item Level: 84
--------
{ Implicit Modifier }
+5% chance to Suppress Spell Damage
(40% of Damage from Suppressed Hits and Ailments they inflict is prevented)
--------
{ Prefix Modifier "Wasp's" (Tier: 3) - Defences, Evasion, Energy Shield }
31(27-32)% increased Evasion and Energy Shield
13(12-13)% increased Stun and Block Recovery
{ Suffix Modifier "of Thick Skin" (Tier: 6) }
11(11-13)% increased Stun and Block Recovery
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

    private sealed class Fixture
    {
        private Fixture(
            PathOfExileTradeStatCatalog statCatalog,
            FakeWindow window,
            FakeSearchClient searchClient,
            PriceCheckerSearchController controller)
        {
            StatCatalog = statCatalog;
            Window = window;
            SearchClient = searchClient;
            Controller = controller;
        }

        private PathOfExileTradeStatCatalog StatCatalog { get; }

        public FakeWindow Window { get; }

        public FakeSearchClient SearchClient { get; }

        private PriceCheckerSearchController Controller { get; }

        public static Fixture Create(PathOfExileTradeStatCatalog statCatalog)
        {
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
                new FakeFilterCatalogProvider(FilterCatalog()));
            var window = new FakeWindow();
            var controller = new PriceCheckerSearchController(service);
            controller.AttachWindow(window);
            window.SetLeague(League);
            return new Fixture(statCatalog, window, searchClient, controller);
        }

        public TradeSearchDraft OpenText(string itemText)
        {
            var catalog = LoadGameDataCatalog();
            var parsed = new ItemTextParser().Parse(itemText);
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
            var draft = draftResult.Draft!;
            Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));
            return draft;
        }

        public void SelectRow(string textFragment)
        {
            var row = Assert.Single(Window.CurrentSearchState!.Modifiers, row =>
                row.Text.Contains(textFragment, StringComparison.Ordinal));
            Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: true);
        }

        public Task SearchAsync()
        {
            return Controller.SearchAsync();
        }

        public string SingleSearchJson()
        {
            Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, Window.CurrentSearchState!.Status);
            var call = Assert.Single(SearchClient.Calls);
            return PathOfExileTradeJson.SerializeSearchRequest(call.Request!);
        }

        private static PathOfExileTradeFilterCatalog FilterCatalog()
        {
            return new PathOfExileTradeFilterCatalog(
            [
                Category(0, "weapon.wand", "Wand"),
                Category(1, "armour.shield", "Shield"),
                Category(2, "accessory.ring", "Ring"),
                Category(3, "accessory.belt", "Belt"),
            ]);
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
    }

    private sealed record SearchCall(PathOfExileTradeSearchRequest? Request);

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
            Calls.Add(new SearchCall(request));
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
        public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;
        public event EventHandler<PriceCheckerLeagueChangedEventArgs>? LeagueChanged;
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

        public PriceCheckerPlacement? GetDisplayedPlacement() => null;

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

        public void SetLeague(string leagueIdentifier)
        {
            LeagueChanged?.Invoke(this, new PriceCheckerLeagueChangedEventArgs(leagueIdentifier));
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

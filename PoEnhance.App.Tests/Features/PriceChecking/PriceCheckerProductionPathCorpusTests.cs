using System.Text.RegularExpressions;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerProductionPathCorpusTests
{
    [Fact]
    public void ShowOrUpdate_MorbidBiteReaverAxe_PreservesCopiedRareIdentityAndBothModifiers()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenFixture(3);

        Assert.Equal("Rare", snapshot.AfterParse.Rarity);
        Assert.Equal("Morbid Bite", snapshot.AfterParse.DisplayName);
        Assert.Equal("Reaver Axe", snapshot.AfterParse.BaseType);
        Assert.Equal(2, snapshot.AfterParse.Modifiers.Count);
        Assert.Equal(2, snapshot.AfterParse.Modifiers.Sum(modifier => modifier.Effects.Count));
        Assert.Contains(
            snapshot.AfterParse.Modifiers.SelectMany(modifier => modifier.Effects),
            effect => effect.Text.Contains("Physical Damage", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.AfterParse.Modifiers.SelectMany(modifier => modifier.Effects),
            effect => effect.Text.Contains("increased Attack Speed", StringComparison.Ordinal));

        if (snapshot.BaseResolution.Status is ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable)
        {
            Assert.Equal("Reaver Axe", snapshot.BaseResolution.ResolvedBaseName);
        }

        Assert.Equal("Rare", snapshot.Draft.Rarity);
        Assert.Equal("Morbid Bite", snapshot.Draft.DisplayName);
        Assert.Equal("Reaver Axe", snapshot.Draft.ParsedBaseType);
        Assert.Equal("Reaver Axe", snapshot.Draft.Base.Observed?.ExactBaseName);
        Assert.Equal("One Hand Axes", snapshot.Draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.Category, snapshot.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(2, snapshot.Draft.ModifierFilters.Count);

        Assert.Equal("Rare", snapshot.WindowState.Draft.Rarity);
        Assert.Equal("Morbid Bite", snapshot.WindowState.Draft.DisplayName);
        Assert.Equal("Reaver Axe", snapshot.WindowState.Draft.ParsedBaseType);
        Assert.Equal(2, snapshot.SearchState.Modifiers.Count);
        Assert.Contains(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("Physical Damage", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("increased Attack Speed", StringComparison.Ordinal));
        Assert.DoesNotContain(
            snapshot.SearchState.Modifiers,
            row => string.Equals(row.Text, "Reaver Axe of Celebration", StringComparison.Ordinal));
        Assert.DoesNotContain(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("Unscalable Value", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.Draft.ModifierFilters,
            modifier => modifier.OriginalText.Contains("Physical Damage", StringComparison.Ordinal));
    }

    [Fact]
    public void ShowOrUpdate_ReaverAxeOfCelebration_ResolvesMagicSuffixBaseAndPreservesCopiedIdentity()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText("""
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
""");

        Assert.Equal("Magic", snapshot.AfterParse.Rarity);
        Assert.Equal("Reaver Axe of Celebration", snapshot.AfterParse.DisplayName);
        Assert.Null(snapshot.AfterParse.BaseType);
        Assert.Equal("Reaver Axe", snapshot.BaseResolution.ResolvedBaseName);
        Assert.Equal(ItemBaseResolutionStatus.Probable, snapshot.BaseResolution.Status);
        Assert.Equal(1, snapshot.AfterParse.Modifiers.Sum(modifier => modifier.Effects.Count));

        Assert.Equal("Magic", snapshot.Draft.Rarity);
        Assert.Equal("Reaver Axe of Celebration", snapshot.Draft.DisplayName);
        Assert.Null(snapshot.Draft.ParsedBaseType);
        Assert.Equal("Reaver Axe", snapshot.Draft.Base.ResolvedBaseName);
        Assert.Equal("Reaver Axe", snapshot.Draft.Base.Observed?.ExactBaseName);
        Assert.Equal("One Hand Axes", snapshot.Draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.Category, snapshot.Draft.Base.ActiveCriterion?.Mode);
        Assert.Single(snapshot.Draft.ModifierFilters);

        Assert.Equal("Magic", snapshot.WindowState.Draft.Rarity);
        Assert.Equal("Reaver Axe of Celebration", snapshot.WindowState.Draft.DisplayName);
        Assert.Single(snapshot.SearchState.Modifiers);
        Assert.Contains(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("increased Attack Speed", StringComparison.Ordinal));
    }

    [Fact]
    public void ShowOrUpdate_WaspsSupremeSpikedShield_ResolvesMagicPrefixSuffixBaseAndPreservesModifierRows()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText("""
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
""");

        Assert.Equal("Magic", snapshot.AfterParse.Rarity);
        Assert.Equal("Wasp's Supreme Spiked Shield of Thick Skin", snapshot.AfterParse.DisplayName);
        Assert.Null(snapshot.AfterParse.BaseType);
        Assert.Equal("Supreme Spiked Shield", snapshot.BaseResolution.ResolvedBaseName);
        Assert.Equal(ItemBaseResolutionStatus.Probable, snapshot.BaseResolution.Status);
        Assert.Equal(4, snapshot.AfterParse.Modifiers.Sum(modifier => modifier.Effects.Count));

        Assert.Equal("Magic", snapshot.Draft.Rarity);
        Assert.Equal("Wasp's Supreme Spiked Shield of Thick Skin", snapshot.Draft.DisplayName);
        Assert.Null(snapshot.Draft.ParsedBaseType);
        Assert.Equal("Supreme Spiked Shield", snapshot.Draft.Base.ResolvedBaseName);
        Assert.Equal("Supreme Spiked Shield", snapshot.Draft.Base.Observed?.ExactBaseName);
        Assert.Equal("Shield", snapshot.Draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.Category, snapshot.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(4, snapshot.Draft.ModifierFilters.Count);

        Assert.Equal("Magic", snapshot.WindowState.Draft.Rarity);
        Assert.Equal("Wasp's Supreme Spiked Shield of Thick Skin", snapshot.WindowState.Draft.DisplayName);
        Assert.Equal(4, snapshot.SearchState.Modifiers.Count);
        Assert.Contains(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("chance to Suppress Spell Damage", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("increased Evasion and Energy Shield", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("13(12-13)% increased Stun and Block Recovery", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("11(11-13)% increased Stun and Block Recovery", StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> OpeningStateFixtures()
    {
        yield return
        [
            0,
            "Necrotic Armour",
            "Normal",
            "Necrotic Armour",
            "Necrotic Armour",
            "Body Armour",
            Array.Empty<string>(),
        ];
        yield return
        [
            2,
            "Golem Fletch Ranger Bow",
            "Rare",
            "Golem Fletch",
            "Ranger Bow",
            "Bow",
            new[]
            {
                "Cold Damage",
                "Fire Damage",
                "Lightning Damage",
                "Dexterity",
            },
        ];
        yield return
        [
            4,
            "Wrath Cry Blasting Wand",
            "Rare",
            "Wrath Cry",
            "Blasting Wand",
            "Wand",
            new[]
            {
                "Cannot roll Caster Modifiers",
                "Lightning Damage to Spells",
                "Cold Damage",
                "chance to Shock",
                "increased Lightning Damage",
                "Gain 17(12-18) Life per Enemy Killed",
            },
        ];
        yield return
        [
            7,
            "Eagle Spiral Organic Ring",
            "Rare",
            "Eagle Spiral",
            "Organic Ring",
            "Ring",
            new[]
            {
                "additional Physical Damage Reduction",
                "Cannot roll Modifiers of Non-Physical Damage Types",
                "Adds 6(5-7) to 12(11-12) Physical Damage to Attacks",
                "Evasion Rating",
                "maximum Mana",
                "Dexterity",
            },
        ];
    }

    [Theory]
    [MemberData(nameof(OpeningStateFixtures))]
    public void ShowOrUpdate_RealCopiedItems_UseProductionOpeningState(
        int fixtureIndex,
        string label,
        string expectedRarity,
        string expectedName,
        string expectedBase,
        string expectedCategory,
        IReadOnlyList<string> expectedRowFragments)
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenFixture(fixtureIndex);

        Assert.Equal(expectedRarity, snapshot.Draft.Rarity);
        Assert.Equal(expectedName, snapshot.Draft.DisplayName);
        Assert.Equal(expectedBase, snapshot.Draft.ParsedBaseType);
        Assert.Equal(expectedBase, snapshot.Draft.Base.Observed?.ExactBaseName);
        Assert.Equal(expectedCategory, snapshot.Draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.Category, snapshot.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(expectedRowFragments.Count, snapshot.SearchState.Modifiers.Count);
        foreach (var expectedRowFragment in expectedRowFragments)
        {
            Assert.Contains(
                snapshot.SearchState.Modifiers,
                row => row.Text.Contains(expectedRowFragment, StringComparison.Ordinal));
        }

        Assert.All(snapshot.SearchState.Modifiers, row =>
            Assert.DoesNotContain("Unscalable Value", row.Text, StringComparison.Ordinal));
        Assert.DoesNotContain(
            snapshot.SearchState.Modifiers,
            row => row.Text.StartsWith("(", StringComparison.Ordinal) ||
                string.Equals(row.Text, "Our flesh longs to move as one.", StringComparison.Ordinal));
        Assert.Equal(label, label);
    }

    private sealed record ProductionPathSnapshot(
        ParsedItem AfterParse,
        ItemBaseResolutionResult BaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> ModifierResolutions,
        TradeSearchDraft Draft,
        PriceCheckerWindowState WindowState,
        PriceCheckerSearchViewState SearchState);

    private sealed class ProductionPathHarness : IDisposable
    {
        private readonly TempDirectory tempDirectory;
        private readonly ParsedItemGameDataDisplayService gameDataDisplayService = new();
        private readonly ItemTextParser parser = new();
        private readonly FakeWindowFactory windowFactory;

        private ProductionPathHarness(
            TempDirectory tempDirectory,
            GameDataCatalog catalog,
            PriceCheckerWindowController controller,
            FakeWindowFactory windowFactory)
        {
            this.tempDirectory = tempDirectory;
            Catalog = catalog;
            Controller = controller;
            this.windowFactory = windowFactory;
        }

        private GameDataCatalog Catalog { get; }

        private PriceCheckerWindowController Controller { get; }

        public static ProductionPathHarness Create()
        {
            var tempDirectory = TempDirectory.Create();
            var windowFactory = new FakeWindowFactory();
            var controller = new PriceCheckerWindowController(
                new FakeBoundsProvider(),
                new PriceCheckerPlacementCalculator(),
                new PriceCheckerPlacementStore(Path.Combine(tempDirectory.Path, "placement.json")),
                windowFactory,
                new CoreTradeSearchDraftMapperAdapter(),
                new CoreTradeSearchDraftValidatorAdapter(),
                new FakeForegroundWindowDetector(),
                new FakeDeferredActionScheduler(),
                new PriceCheckerSearchController(new FakePriceCheckService()));

            return new ProductionPathHarness(
                tempDirectory,
                LoadGameDataCatalog(),
                controller,
                windowFactory);
        }

        public ProductionPathSnapshot OpenFixture(int fixtureIndex)
        {
            return OpenText(CopiedItemCorpus.LoadItems()[fixtureIndex]);
        }

        public ProductionPathSnapshot OpenText(string itemText)
        {
            var parsed = parser.Parse(itemText);
            var baseResolution = gameDataDisplayService.ResolveItemBase(parsed, Catalog).Result;
            Assert.NotNull(baseResolution);
            var modifierResolutions = gameDataDisplayService
                .ResolveModifierCandidates(parsed, Catalog, baseResolution)
                .Results
                .Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();

            var updateResult = Controller.ShowOrUpdate(
                parsed,
                baseResolution,
                modifierResolutions,
                Catalog);

            Assert.True(updateResult.IsSuccess, updateResult.Diagnostic);
            var window = Assert.Single(windowFactory.CreatedWindows);
            Assert.NotNull(window.CurrentState);
            Assert.NotNull(window.CurrentSearchState);

            return new ProductionPathSnapshot(
                parsed,
                baseResolution,
                modifierResolutions,
                window.CurrentState!.Draft,
                window.CurrentState,
                window.CurrentSearchState!);
        }

        public void Dispose()
        {
            tempDirectory.Dispose();
        }
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

#pragma warning disable CS0067
    private sealed class FakeWindow : IPriceCheckerWindow
    {
        public event EventHandler? Closed;
        public event EventHandler? PanelActivated;
        public event EventHandler? PanelDeactivated;
        public event EventHandler? PanelInteraction;
        public event EventHandler? SearchRequested;
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
    }
#pragma warning restore CS0067

    private sealed class FakeWindowFactory : IPriceCheckerWindowFactory
    {
        public List<FakeWindow> CreatedWindows { get; } = [];

        public IPriceCheckerWindow CreateWindow()
        {
            var window = new FakeWindow();
            CreatedWindows.Add(window);
            return window;
        }
    }

    private sealed class FakeBoundsProvider : IPathOfExileClientBoundsProvider
    {
        public bool TryGetClientBounds(out PathOfExileClientBounds clientBounds)
        {
            clientBounds = new PathOfExileClientBounds(
                Left: 100,
                Top: 50,
                Width: 1000,
                Height: 800,
                DisplayDeviceName: @"\\.\DISPLAY1",
                DpiScaleX: 1,
                DpiScaleY: 1);
            return true;
        }
    }

    private sealed class FakeForegroundWindowDetector : IPathOfExileForegroundWindowDetector
    {
        public bool IsPathOfExileForegroundWindow() => true;
    }

    private sealed class FakeDeferredActionScheduler : IPriceCheckerDeferredActionScheduler
    {
        public void Schedule(Action action)
        {
        }
    }

    private sealed class FakePriceCheckService : IPathOfExileTradePriceCheckService
    {
        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PathOfExileTradePriceCheckResult
            {
                IsSuccess = true,
                Stage = PathOfExileTradePriceCheckStage.Completed,
            });
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"poenhance-production-path-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

using System.Text.RegularExpressions;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerCopiedItemCorpusTests
{
    private static readonly string[] ForbiddenRows =
    [
        "(The Damage Types are Physical, Fire, Cold, Lightning, and Chaos)",
        "Our flesh longs to move as one.",
        "Place into an allocated Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket.",
        "Unidentified",
        "Searing Exarch Item",
        "Eater of Worlds Item",
    ];

    public static IEnumerable<object[]> ExpectedPriceCheckerRows()
    {
        yield return [3, "Morbid Bite Reaver Axe", 2];
        yield return [4, "Blasting Wand", 6];
        yield return [7, "Organic Ring", 6];
        yield return [8, "Gladiator Plate", 5];
        yield return [9, "Conjurer Boots", 7];
        yield return [10, "Stygian Vise", 6];
        yield return [11, "Marshall's Brigandine", 10];
        yield return [13, "Supreme Spiked Shield", 8];
        yield return [14, "Cryonic Ring", 2];
    }

    [Theory]
    [MemberData(nameof(ExpectedPriceCheckerRows))]
    public void UpdateCurrentDraft_RealCopiedItemsExposeOneRowPerLogicalEffect(
        int fixtureIndex,
        string label,
        int expectedRowCount)
    {
        var draft = CreateDraft(fixtureIndex);
        var fixture = SearchFixture.Create();

        fixture.Controller.UpdateCurrentDraft(draft, TradeSearchValidationResult.FromDiagnostics([]));

        var rows = fixture.Window.CurrentSearchState?.Modifiers ?? [];
        Assert.Equal(expectedRowCount, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.DoesNotContain("Unscalable Value", row.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(ForbiddenRows, forbidden => string.Equals(forbidden, row.Text, StringComparison.Ordinal));
        });
        Assert.Equal(label, label);
    }

    [Fact]
    public void UpdateCurrentDraft_HybridRowsRetainSourceModifierProvenance()
    {
        var draft = CreateDraft(8);

        var hybridRows = draft.ModifierFilters
            .Where(component => component.OriginalText is "+139(97-144) to Armour" or "+37(34-38) to maximum Life")
            .ToArray();

        Assert.Equal(2, hybridRows.Length);
        Assert.All(hybridRows, row => Assert.Equal(1, row.SourceModifierIndex));
        Assert.Equal([0, 1], hybridRows.Select(row => row.SourceLineIndex));
        Assert.Equal([0, 1], hybridRows.Select(row => row.SourceComponentIndex));
        Assert.NotEqual(hybridRows[0].ComponentId, hybridRows[1].ComponentId);
    }

    private static TradeSearchDraft CreateDraft(int fixtureIndex)
    {
        var item = CopiedItemCorpus.LoadItems()[fixtureIndex];
        var parsed = new ItemTextParser().Parse(item);
        var result = new TradeSearchDraftMapper().CreateDraft(parsed);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Draft);
        return result.Draft;
    }

    private sealed class FakePriceCheckService : IPathOfExileTradePriceCheckService
    {
        public Task<PathOfExileTradeFilterCatalogProviderResult> InitializeFilterCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeFilterCatalogProviderResult());

        public TradeSearchDraft ResolveEffectiveDraft(TradeSearchDraft draft) => draft;

        public Task<string?> LoadCategoryDisplayLabelAsync(
            TradeSearchDraft draft,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

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

        public Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
            string? searchQueryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Load More is not expected in this corpus test.");
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
    }
#pragma warning restore CS0067

    private sealed record SearchFixture(
        FakeWindow Window,
        PriceCheckerSearchController Controller)
    {
        public static SearchFixture Create()
        {
            var window = new FakeWindow();
            var controller = new PriceCheckerSearchController(
                new FakePriceCheckService(),
                global::PoEnhance.App.Infrastructure.Settings.ApplicationLeagueSetting.CreateTransient("Mirage"));
            controller.AttachWindow(window);
            return new SearchFixture(window, controller);
        }
    }

    private static class CopiedItemCorpus
    {
        private static readonly Regex ItemBoundary = new(
            @"\r?\n\s*\r?\n(?=Item Class:)",
            RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> LoadItems()
        {
            var corpusPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Items", "advanced-real-items-corpus.txt");
            var corpus = File.ReadAllText(corpusPath);
            var items = ItemBoundary
                .Split(corpus.TrimEnd('\r', '\n'))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

            Assert.Equal(15, items.Length);
            return items;
        }
    }
}

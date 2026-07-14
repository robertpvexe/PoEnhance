using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerSearchControllerTests
{
    [Fact]
    public void UpdateCurrentDraft_PreparesSearchStateWithoutCallingService()
    {
        var fixture = SearchFixture.Create();

        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Controller.CurrentViewState.Status);
        Assert.True(fixture.Controller.CurrentViewState.CanSearch);
        Assert.Empty(fixture.PriceCheckService.Calls);
    }

    [Fact]
    public async Task SearchAsync_CallsServiceOnceWithCurrentDraftValidationAndTrimmedLeague()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Armoured Shell");
        var validation = ValidationSuccess();
        fixture.Controller.UpdateCurrentDraft(draft, validation);
        fixture.Window.SetLeague("  Mercenaries  ");

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Same(draft, call.Draft);
        Assert.Same(validation, call.ValidationResult);
        Assert.Equal("Mercenaries", call.LeagueIdentifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SearchAsync_BlankLeaguePreventsExecution(string leagueIdentifier)
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        fixture.Window.SetLeague(leagueIdentifier);

        await fixture.Controller.SearchAsync();

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("League is required.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_InvalidDraftPreventsExecution()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell"),
            TradeSearchValidationResult.FromDiagnostics(
            [
                new TradeSearchValidationDiagnostic(
                    "LOCAL_INVALID",
                    TradeSearchValidationSeverity.Error,
                    "Invalid draft."),
            ]));

        await fixture.Controller.SearchAsync();

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal("Select a supported Trade search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_SelectedModifierCallsServiceWhenDraftIsLocallyValid()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell", selectedModifier: true), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
    }

    [Fact]
    public async Task SearchAsync_SelectedModifierLocalValidationFailureUsesSafeModifierMessage()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", selectedModifier: true),
            TradeSearchValidationResult.FromDiagnostics(
            [
                new TradeSearchValidationDiagnostic(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved,
                    TradeSearchValidationSeverity.Error,
                    "Selected modifier unresolved.",
                    ModifierFilterIndex: 0),
            ]));

        await fixture.Controller.SearchAsync();

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Selected modifier is not available in Trade search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_LoadingDisablesSearchAndRepeatedClickDoesNotStartSecondCall()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        var firstSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        var secondSearch = fixture.Controller.SearchAsync();

        Assert.False(fixture.Window.CurrentSearchState?.CanSearch);
        Assert.Equal(PriceCheckerSearchViewStatus.Loading, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Searching...", fixture.Window.CurrentSearchState?.Message);
        Assert.Single(fixture.PriceCheckService.Calls);

        completion.SetResult(SuccessResult([Offer("id-1")], total: 1));
        await firstSearch;
        await secondSearch;
    }

    [Fact]
    public async Task SearchAsync_SuccessDisplaysFetchedOffersInOrderCountTotalAndInexact()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [
                Offer("id-1", amount: 1.25m, currency: "divine"),
                Offer("id-2", amount: 10m, currency: "chaos"),
            ],
            total: 148,
            inexact: true);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var state = fixture.Window.CurrentSearchState;
        Assert.Equal(PriceCheckerSearchViewStatus.Success, state?.Status);
        Assert.Equal("Showing 2 of 148 offers (inexact)", state?.Summary);
        Assert.Equal(["1.25 divine", "10 chaos"], state?.Offers.Select(offer => offer.PriceText));
    }

    [Fact]
    public async Task SearchAsync_ZeroResultSuccessDisplaysNoOffersFound()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([], total: 0);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("No offers found.", fixture.Window.CurrentSearchState?.Summary);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
    }

    [Fact]
    public async Task SearchAsync_OfferRowsUsePriceSellerAndIndexedFallbacks()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [
                Offer(
                    "id-1",
                    amount: null,
                    currency: null,
                    lastCharacterName: "LastChar",
                    accountName: "Account",
                    onlineStatus: "online",
                    onlineLeague: "Mercenaries",
                    rawIndexed: "raw-indexed"),
                Offer(
                    "id-2",
                    amount: 3m,
                    currency: "divine",
                    lastCharacterName: null,
                    accountName: "AccountOnly"),
                Offer(
                    "id-3",
                    amount: 7.5m,
                    currency: "chaos",
                    lastCharacterName: null,
                    accountName: null),
            ],
            total: 3);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var offers = fixture.Window.CurrentSearchState?.Offers ?? [];
        Assert.Equal("No listed price", offers[0].PriceText);
        Assert.Equal("LastChar", offers[0].SellerText);
        Assert.Equal("online (Mercenaries)", offers[0].OnlineStatusText);
        Assert.Equal("raw-indexed", offers[0].IndexedText);
        Assert.Equal("AccountOnly", offers[1].SellerText);
        Assert.Equal("Unknown seller", offers[2].SellerText);
    }

    [Fact]
    public async Task UpdateCurrentDraft_CancelsActiveRequestClearsOldOffersAndPreventsLateOverwrite()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("old")], total: 1);
        fixture.Controller.UpdateCurrentDraft(Draft("Old Loop"), ValidationSuccess());
        await fixture.Controller.SearchAsync();
        Assert.NotEmpty(fixture.Window.CurrentSearchState?.Offers ?? []);

        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        var activeSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 2);

        fixture.Controller.UpdateCurrentDraft(Draft("New Loop"), ValidationSuccess());

        Assert.True(fixture.PriceCheckService.Calls[1].CancellationToken.IsCancellationRequested);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);

        completion.SetResult(SuccessResult([Offer("late-old")], total: 1));
        await activeSearch;

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
    }

    [Fact]
    public async Task WindowClose_CancelsActiveRequestAndPreventsLateUiUpdate()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        var activeSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        fixture.Window.Close();

        Assert.True(fixture.PriceCheckService.Calls[0].CancellationToken.IsCancellationRequested);
        completion.SetResult(FailureResult("Provider exploded."));
        await activeSearch;

        Assert.NotEqual(PriceCheckerSearchViewStatus.ProviderOrTransportError, fixture.Window.CurrentSearchState?.Status);
    }

    [Fact]
    public async Task SearchAsync_CancellationResultDoesNotDisplayProviderFailure()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Search,
            IsCancelled = true,
        };
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.Cancelled, fixture.Window.CurrentSearchState?.Status);
        Assert.NotEqual("Trade request failed. Try again later.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_FailureDisplaysSafeConciseProviderMessage()
    {
        var fixture = SearchFixture.Create();
        var longProviderMessage = $"Provider said no.{Environment.NewLine}{new string('x', 240)}";
        fixture.PriceCheckService.Result = FailureResult(
            longProviderMessage,
            sourceCode: PathOfExileTradeHttpDiagnosticCodes.ProviderDeclaredError,
            providerCode: "3");
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var message = fixture.Window.CurrentSearchState?.Message ?? string.Empty;
        Assert.Equal(PriceCheckerSearchViewStatus.ProviderOrTransportError, fixture.Window.CurrentSearchState?.Status);
        Assert.StartsWith("Trade returned an error: Provider said no.", message, StringComparison.Ordinal);
        Assert.DoesNotContain(Environment.NewLine, message);
        Assert.True(message.Length <= 190);
    }

    [Fact]
    public async Task SearchAsync_CatalogFailureUsesTradeDefinitionsMessage()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.CatalogLoad,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed,
                    "Stats failed.",
                    PathOfExileTradePriceCheckStage.CatalogLoad),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell", selectedModifier: true), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ProviderOrTransportError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Could not load Trade modifier definitions.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_AmbiguousSelectedModifierUsesSafeMappingMessage()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.ModifierMapping,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed,
                    "Ambiguous.",
                    PathOfExileTradePriceCheckStage.ModifierMapping,
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell", selectedModifier: true), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Selected modifier matches multiple Trade filters.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_UnmatchedSelectedModifierUsesSafeMappingMessage()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.ModifierMapping,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed,
                    "Not found.",
                    PathOfExileTradePriceCheckStage.ModifierMapping,
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell", selectedModifier: true), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Selected modifier is not available in Trade search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_SearchBecomesAvailableAgainAfterCompletionWhenInputIsValid()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.True(fixture.Window.CurrentSearchState?.CanSearch);
    }

    [Fact]
    public void UpdateCurrentDraft_PreservesLeagueDuringSameWindowItemChanges()
    {
        var fixture = SearchFixture.Create();
        fixture.Window.SetLeague("Mercenaries");

        fixture.Controller.UpdateCurrentDraft(Draft("First Loop"), ValidationSuccess());
        fixture.Controller.UpdateCurrentDraft(Draft("Second Loop"), ValidationSuccess());

        Assert.Equal("Mercenaries", fixture.Window.CurrentSearchState?.LeagueIdentifier);
    }

    private static TradeSearchDraft Draft(string name, bool selectedModifier = false)
    {
        return new TradeSearchDraft
        {
            ItemClass = "Body Armours",
            Rarity = "Rare",
            DisplayName = name,
            ParsedBaseType = "Titan Plate",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.titan-plate",
                ResolvedBaseName = "Titan Plate",
            },
            ModifierFilters = selectedModifier
                ?
                [
                    new TradeModifierFilterDraft
                    {
                        OriginalText = "+10 to maximum Life",
                        ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                        ResolvedModifierId = "mod.life",
                        IsSelected = true,
                    },
                ]
                : [],
        };
    }

    private static TradeSearchValidationResult ValidationSuccess()
    {
        return TradeSearchValidationResult.FromDiagnostics([]);
    }

    private static PathOfExileTradePriceCheckResult SuccessResult(
        IReadOnlyList<PathOfExileTradeFetchedOffer> offers,
        int total,
        bool? inexact = null)
    {
        return new PathOfExileTradePriceCheckResult
        {
            IsSuccess = true,
            Stage = PathOfExileTradePriceCheckStage.Completed,
            SearchQueryId = "query-1",
            ProviderTotal = total,
            Inexact = inexact,
            Offers = offers,
        };
    }

    private static PathOfExileTradePriceCheckResult FailureResult(
        string message,
        string sourceCode = PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
        string? providerCode = null)
    {
        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Search,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SearchFailed,
                    message,
                    PathOfExileTradePriceCheckStage.Search,
                    sourceCode,
                    ProviderCode: providerCode),
            ],
        };
    }

    private static PathOfExileTradeFetchedOffer Offer(
        string id,
        decimal? amount = 1m,
        string? currency = "chaos",
        string? lastCharacterName = "Seller",
        string? accountName = "Account",
        string? onlineStatus = null,
        string? onlineLeague = null,
        string? rawIndexed = null)
    {
        return new PathOfExileTradeFetchedOffer
        {
            Id = id,
            Item = new PathOfExileTradeFetchedItem
            {
                Name = "Armoured Shell",
                TypeLine = "Titan Plate",
            },
            Listing = new PathOfExileTradeListing
            {
                RawIndexed = rawIndexed,
                Account = accountName is null && lastCharacterName is null
                    ? null
                    : new PathOfExileTradeListingAccount
                    {
                        Name = accountName,
                        LastCharacterName = lastCharacterName,
                        Online = onlineStatus is null && onlineLeague is null
                            ? null
                            : new PathOfExileTradeListingOnlineState
                            {
                                Status = onlineStatus,
                                League = onlineLeague,
                            },
                    },
                Price = amount is null && currency is null
                    ? null
                    : new PathOfExileTradeListingPrice
                    {
                        Amount = amount,
                        Currency = currency,
                    },
            },
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed record PriceCheckCall(
        TradeSearchDraft? Draft,
        TradeSearchValidationResult? ValidationResult,
        string? LeagueIdentifier,
        CancellationToken CancellationToken);

    private sealed class SearchFixture
    {
        private SearchFixture(
            FakeWindow window,
            FakePriceCheckService priceCheckService,
            PriceCheckerSearchController controller)
        {
            Window = window;
            PriceCheckService = priceCheckService;
            Controller = controller;
        }

        public FakeWindow Window { get; }

        public FakePriceCheckService PriceCheckService { get; }

        public PriceCheckerSearchController Controller { get; }

        public static SearchFixture Create()
        {
            var window = new FakeWindow();
            var priceCheckService = new FakePriceCheckService();
            var controller = new PriceCheckerSearchController(priceCheckService);
            controller.AttachWindow(window);
            return new SearchFixture(window, priceCheckService, controller);
        }
    }

    private sealed class FakePriceCheckService : IPathOfExileTradePriceCheckService
    {
        public List<PriceCheckCall> Calls { get; } = [];

        public PathOfExileTradePriceCheckResult Result { get; set; } =
            SuccessResult([], total: 0);

        public Func<PriceCheckCall, Task<PathOfExileTradePriceCheckResult>>? Handler { get; set; }

        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            var call = new PriceCheckCall(draft, validationResult, leagueIdentifier, cancellationToken);
            Calls.Add(call);
            return Handler is null
                ? Task.FromResult(Result)
                : Handler(call);
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

        public event EventHandler<PriceCheckerLeagueChangedEventArgs>? LeagueChanged;

        public event EventHandler<bool>? PinStateChanged;

        public event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;

        public event EventHandler? HorizontalDragCompleted;

        public event EventHandler? ResetPositionRequested;

        public bool IsClosed { get; private set; }

        public bool IsPinned { get; private set; }

        public PriceCheckerWindowState? CurrentState { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public PriceCheckerSearchViewState? CurrentSearchState { get; private set; }

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

        public void RaiseSearchRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }
    }
#pragma warning restore CS0067
}

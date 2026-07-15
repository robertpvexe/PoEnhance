using System.Globalization;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
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
    public void AttachWindow_CleanProfileDefaultsLeagueToMirage()
    {
        var fixture = SearchFixture.Create();

        Assert.Equal("Mirage", fixture.Window.CurrentSearchState?.LeagueIdentifier);
    }

    [Fact]
    public async Task SearchAsync_CallsServiceOnceWithCurrentDraftValidationAndMirage()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Armoured Shell");
        var validation = ValidationSuccess();
        fixture.Controller.UpdateCurrentDraft(draft, validation);

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Same(draft, call.Draft);
        Assert.Same(validation, call.ValidationResult);
        Assert.Equal("Mirage", call.LeagueIdentifier);
    }

    [Fact]
    public async Task SearchAsync_MirageSessionDoesNotChangeListingModeOrQueryCriteria()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Armoured Shell");
        fixture.Controller.UpdateCurrentDraft(draft, ValidationSuccess());
        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Same(draft, call.Draft);
        Assert.Equal(TradeListingMode.InstantBuyout, call.Draft?.ListingMode);
        Assert.Equal(ItemBaseResolutionStatus.Exact, call.Draft?.Base.Status);
        Assert.Equal("base.titan-plate", call.Draft?.Base.ResolvedBaseId);
        Assert.Equal("Rare", call.Draft?.Rarity);
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
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.True(Assert.Single(call.Draft?.ModifierFilters ?? []).IsSelected);
        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
    }

    [Fact]
    public async Task SearchAsync_SelectedLocallyUnresolvedModifierUsesProviderMappingFailure()
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
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life", resolvedModifierId: null)]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Contains(
            call.ValidationResult?.Diagnostics ?? [],
            diagnostic =>
                diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved &&
                diagnostic.Severity == TradeSearchValidationSeverity.Warning);
        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Selected modifier is not available in Trade search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public void ModifierSelectionChanged_LocallyUnresolvedModifierKeepsSearchEnabledBeforeProviderMapping()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life", resolvedModifierId: null)]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.True(fixture.Window.CurrentSearchState?.CanSearch);
        Assert.Equal("Ready to search.", fixture.Window.CurrentSearchState?.Message);
        Assert.Contains(
            fixture.Window.CurrentState?.ValidationResult.Diagnostics ?? [],
            diagnostic =>
                diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved &&
                diagnostic.Severity == TradeSearchValidationSeverity.Warning);
    }

    [Fact]
    public void UpdateCurrentDraft_DisplaysModifiersInDraftOrderUncheckedAndWithSectionLabels()
    {
        var fixture = SearchFixture.Create();

        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier("+12% to Fire Resistance", ParsedModifierKind.Implicit),
                    Modifier("+10 to maximum Life", ParsedModifierKind.Prefix),
                    Modifier("+20% to Cold Resistance", ParsedModifierKind.Suffix),
                ]),
            ValidationSuccess());

        var modifiers = fixture.Window.CurrentSearchState?.Modifiers ?? [];
        Assert.Equal(
            ["+12% to Fire Resistance", "+10 to maximum Life", "+20% to Cold Resistance"],
            modifiers.Select(modifier => modifier.Text));
        Assert.Equal(["Implicit", "Prefix", "Suffix"], modifiers.Select(modifier => modifier.SectionLabel));
        Assert.All(modifiers, modifier => Assert.False(modifier.IsSelected));
        Assert.Equal(0, fixture.Window.CurrentSearchState?.SelectedModifierCount);
    }

    [Fact]
    public void ModifierSelectionChanged_SelectsOnlyRequestedModifierUpdatesCountAndDoesNotCallService()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+20% to Cold Resistance"),
                    Modifier("+30% to Fire Resistance"),
                ]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(1, isSelected: true);

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal([false, true, false], fixture.Window.CurrentSearchState?.Modifiers.Select(modifier => modifier.IsSelected));
        Assert.Equal(1, fixture.Window.CurrentSearchState?.SelectedModifierCount);
        Assert.Equal([false, true, false], fixture.Window.CurrentState?.Draft.ModifierFilters.Select(modifier => modifier.IsSelected));
    }

    [Fact]
    public void ModifierSelectionChanged_UnselectingRestoresModifierToUnselected()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);

        Assert.Equal(0, fixture.Window.CurrentSearchState?.SelectedModifierCount);
        Assert.False(Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).IsSelected);
    }

    [Fact]
    public void ModifierSelectionChanged_DuplicateTextRowsRemainIndependent()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+10 to maximum Life"),
                ]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(1, isSelected: true);

        var modifiers = fixture.Window.CurrentSearchState?.Modifiers ?? [];
        Assert.Equal(["+10 to maximum Life", "+10 to maximum Life"], modifiers.Select(modifier => modifier.Text));
        Assert.Equal([false, true], modifiers.Select(modifier => modifier.IsSelected));
        Assert.Equal([0, 1], modifiers.Select(modifier => modifier.SourceIndex));
    }

    [Fact]
    public async Task SearchAsync_SelectedModifiersPreserveDraftOrderInServiceDraft()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+20% to Cold Resistance"),
                    Modifier("+30% to Fire Resistance"),
                ]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(2, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        await fixture.Controller.SearchAsync();

        var draft = Assert.Single(fixture.PriceCheckService.Calls).Draft;
        Assert.NotNull(draft);
        var selected = draft.ModifierFilters
            .Select((modifier, index) => (modifier, index))
            .Where(pair => pair.modifier.IsSelected)
            .Select(pair => pair.index)
            .ToArray();
        Assert.Equal([0, 2], selected);
    }

    [Fact]
    public async Task SearchAsync_ZeroSelectionsPassesBaseOnlyDraft()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.DoesNotContain(
            Assert.Single(fixture.PriceCheckService.Calls).Draft?.ModifierFilters ?? [],
            modifier => modifier.IsSelected);
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
    public async Task SearchAsync_MapsOneFetchedOfferToFourStructuredColumns()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [Offer(
                "id-1",
                amount: 3m,
                currency: "chaos",
                accountName: "Seller Account",
                itemName: "Armageddon Thirst",
                itemLevel: 72,
                indexed: DateTimeOffset.UtcNow.AddSeconds(-30))],
            total: 1);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var offer = Assert.Single(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.Equal("Armageddon Thirst", offer.ItemName);
        Assert.Equal("Seller Account", offer.SellerAccountName);
        Assert.Equal("1 min ago", offer.ListedText);
        Assert.Equal("72", offer.ItemLevelText);
        Assert.Equal("3 chaos", offer.PriceText);
    }

    [Fact]
    public async Task SearchAsync_DoesNotDisplayDuplicateFetchedResultIds()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [Offer("id-1", amount: 1m), Offer("id-2", amount: 2m)],
            total: 2,
            resultIds: ["id-1", "id-1", "id-2"],
            fetchedResultIds: ["id-1", "id-1", "id-2"]);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(
            ["id-1", "id-2"],
            fixture.Window.CurrentSearchState?.Offers.Select(offer => offer.Id));
    }

    [Fact]
    public async Task LoadMoreAsync_FetchesSuccessiveBatchesWithoutAnotherSearchAndAppendsProviderOrder()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 25).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 25,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(10).Take(10).Reverse()),
            total: 25,
            fetchedResultIds: ids.Skip(10).Take(10).ToArray()));
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(20).Reverse()),
            total: 25,
            fetchedResultIds: ids.Skip(20).ToArray()));
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.True(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Single(fixture.PriceCheckService.Calls);
        fixture.Window.RaiseLoadMoreRequested();
        await WaitUntilAsync(() =>
            fixture.PriceCheckService.LoadMoreCalls.Count == 1 &&
            fixture.Window.CurrentSearchState?.Offers.Count == 20);

        var firstLoadMore = Assert.Single(fixture.PriceCheckService.LoadMoreCalls);
        Assert.Equal("query-1", firstLoadMore.SearchQueryId);
        Assert.Equal(ids.Skip(10).Take(10), firstLoadMore.ResultIds);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Equal(
            Enumerable.Range(1, 20).Select(index => $"{index} chaos"),
            fixture.Window.CurrentSearchState?.Offers.Select(offer => offer.PriceText));

        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(2, fixture.PriceCheckService.LoadMoreCalls.Count);
        Assert.Equal(ids.Skip(20), fixture.PriceCheckService.LoadMoreCalls[1].ResultIds);
        Assert.Equal(25, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Equal(
            25,
            ids.Take(10)
                .Concat(fixture.PriceCheckService.LoadMoreCalls
                .SelectMany(call => call.ResultIds ?? [])
                )
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public async Task LoadMoreAsync_FailurePreservesOffersAndLeavesTheSameBatchForExplicitRetry()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 12,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Fetch,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed,
                    "Fetch failed.",
                    PathOfExileTradePriceCheckStage.Fetch),
            ],
        });
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(10)),
            total: 12,
            fetchedResultIds: ids.Skip(10).ToArray()));
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(10, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.True(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Equal("Could not load more offers. Try again.", fixture.Window.CurrentSearchState?.Message);
        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(ids.Skip(10), fixture.PriceCheckService.LoadMoreCalls[0].ResultIds);
        Assert.Equal(ids.Skip(10), fixture.PriceCheckService.LoadMoreCalls[1].ResultIds);
        Assert.Equal(12, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
    }

    [Fact]
    public async Task SearchAsync_CancelsActiveLoadMoreAndPreventsItsLateCompletionFromAppending()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 20).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 20,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.Controller.UpdateCurrentDraft(Draft("First"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.LoadMoreHandler = _ => completion.Task;
        var loadMore = fixture.Controller.LoadMoreAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.LoadMoreCalls.Count == 1);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        fixture.PriceCheckService.Result = SuccessResult([Offer("new-id", amount: 99m)], total: 1);

        await fixture.Controller.SearchAsync();

        Assert.True(fixture.PriceCheckService.LoadMoreCalls[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(["99 chaos"], fixture.Window.CurrentSearchState?.Offers.Select(offer => offer.PriceText));
        completion.SetResult(SuccessResult(OffersFor(ids.Skip(10)), total: 20));
        await loadMore;

        Assert.Equal(["99 chaos"], fixture.Window.CurrentSearchState?.Offers.Select(offer => offer.PriceText));
        Assert.Equal(2, fixture.PriceCheckService.Calls.Count);
        Assert.Empty(fixture.PriceCheckService.PendingLoadMoreResults);
    }

    [Fact]
    public async Task UpdateCurrentDraft_ClearsPaginationAndMakesLoadMoreUnavailable()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 12,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.Controller.UpdateCurrentDraft(Draft("First"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        fixture.Controller.UpdateCurrentDraft(Draft("Second"), ValidationSuccess());
        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task SearchAsync_ZeroResultSuccessDisplaysNoOffersFound()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([], total: 0);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("No offers found.", fixture.Window.CurrentSearchState?.Message);
        Assert.Empty(fixture.Window.CurrentSearchState?.Summary ?? string.Empty);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
    }

    [Fact]
    public async Task SearchAsync_RepeatedZeroResultsKeepOneMessageAndNoStaleOffers()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("old")], total: 1);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        await fixture.Controller.SearchAsync();
        Assert.NotEmpty(fixture.Window.CurrentSearchState?.Offers ?? []);

        fixture.PriceCheckService.Result = SuccessResult([], total: 0);
        await fixture.Controller.SearchAsync();
        await fixture.Controller.SearchAsync();

        var state = fixture.Window.CurrentSearchState;
        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, state?.Status);
        Assert.Equal("No offers found.", state?.Message);
        Assert.Empty(state?.Summary ?? string.Empty);
        Assert.Empty(state?.Offers ?? []);
    }

    [Fact]
    public async Task SearchAsync_OfferRowsKeepFieldsSeparateAndUseMissingMarkers()
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
                    rawIndexed: "raw-indexed",
                    itemName: "Named item",
                    itemLevel: 85),
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
                    accountName: null,
                    itemName: null,
                    itemLevel: null),
            ],
            total: 3);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var offers = fixture.Window.CurrentSearchState?.Offers ?? [];
        Assert.Equal("—", offers[0].PriceText);
        Assert.Equal("Named item", offers[0].ItemName);
        Assert.Equal("Account", offers[0].SellerAccountName);
        Assert.Equal("—", offers[0].ListedText);
        Assert.Equal("raw-indexed", offers[0].ListedToolTip);
        Assert.Equal("85", offers[0].ItemLevelText);
        Assert.Equal("AccountOnly", offers[1].SellerAccountName);
        Assert.Equal("—", offers[2].ItemName);
        Assert.Equal("—", offers[2].SellerAccountName);
        Assert.Equal("—", offers[2].ItemLevelText);
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
        Assert.NotEqual("No offers found.", fixture.Window.CurrentSearchState?.Message);
        Assert.NotEqual("No offers found.", fixture.Window.CurrentSearchState?.Summary);
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
    public async Task ModifierSelectionChanged_ClearsOldOffersAndProviderErrorWithoutCallingService()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("old")], total: 1);
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());
        await fixture.Controller.SearchAsync();
        Assert.NotEmpty(fixture.Window.CurrentSearchState?.Offers ?? []);
        var callsAfterSuccess = fixture.PriceCheckService.Calls.Count;

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Equal(callsAfterSuccess, fixture.PriceCheckService.Calls.Count);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Ready to search.", fixture.Window.CurrentSearchState?.Message);

        fixture.PriceCheckService.Result = FailureResult("Provider exploded.");
        await fixture.Controller.SearchAsync();
        Assert.Equal(PriceCheckerSearchViewStatus.ProviderOrTransportError, fixture.Window.CurrentSearchState?.Status);
        var callsAfterFailure = fixture.PriceCheckService.Calls.Count;

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);

        Assert.Equal(callsAfterFailure, fixture.PriceCheckService.Calls.Count);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Ready to search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task ModifierSelectionChanged_DuringLoadingCancelsRequestAndPreventsLateOverwrite()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());

        var activeSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.True(fixture.PriceCheckService.Calls[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.True(fixture.Window.CurrentSearchState?.CanSearch);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);

        completion.SetResult(SuccessResult([Offer("late-old")], total: 1));
        await activeSearch;

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
    }

    [Fact]
    public void UpdateCurrentDraft_ReplacesModifierListAndClearsPriorSelectionsEvenForIdenticalText()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "First Loop",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+20% to Cold Resistance"),
                ]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        Assert.Equal(1, fixture.Window.CurrentSearchState?.SelectedModifierCount);

        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Second Loop",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+30% to Fire Resistance"),
                    Modifier("+40% to Lightning Resistance"),
                ]),
            ValidationSuccess());

        var modifiers = fixture.Window.CurrentSearchState?.Modifiers ?? [];
        Assert.Equal(
            ["+10 to maximum Life", "+30% to Fire Resistance", "+40% to Lightning Resistance"],
            modifiers.Select(modifier => modifier.Text));
        Assert.All(modifiers, modifier => Assert.False(modifier.IsSelected));
        Assert.Equal(0, fixture.Window.CurrentSearchState?.SelectedModifierCount);
    }

    [Fact]
    public void ModifierSelectionChanged_PreservesMirageAndPinState()
    {
        var fixture = SearchFixture.Create();
        fixture.Window.SetPinned(true);
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Equal("Mirage", fixture.Window.CurrentSearchState?.LeagueIdentifier);
        Assert.True(fixture.Window.IsPinned);
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
    public void UpdateCurrentDraft_PreservesMirageDuringSameWindowItemChanges()
    {
        var fixture = SearchFixture.Create();

        fixture.Controller.UpdateCurrentDraft(Draft("First Loop"), ValidationSuccess());
        fixture.Controller.UpdateCurrentDraft(Draft("Second Loop"), ValidationSuccess());

        Assert.Equal("Mirage", fixture.Window.CurrentSearchState?.LeagueIdentifier);
    }

    [Fact]
    public async Task BaseCriterionToggleRequested_SwitchesActualDraftBetweenCategoryAndExactBase()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(DraftWithBothBaseCriteria(), ValidationSuccess());

        fixture.Window.RaiseBaseCriterionToggleRequested();

        Assert.Equal(
            BaseSearchMode.ExactBase,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
        await fixture.Controller.SearchAsync();
        Assert.Equal(
            BaseSearchMode.ExactBase,
            Assert.Single(fixture.PriceCheckService.Calls).Draft?.Base.ActiveCriterion?.Mode);

        fixture.Window.RaiseBaseCriterionToggleRequested();

        Assert.Equal(
            BaseSearchMode.Category,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
    }

    [Fact]
    public async Task SearchAsync_StygianForcedExactBaseResultRemainsReflectedInTheCurrentDraft()
    {
        var fixture = SearchFixture.Create();
        var categoryDraft = DraftWithBothBaseCriteria("Belt", "Stygian Vise");
        var forcedExactBase = categoryDraft with
        {
            Base = categoryDraft.Base with
            {
                ActiveCriterion = categoryDraft.Base.AvailableCriteria.ExactBase,
            },
        };
        fixture.PriceCheckService.Result = SuccessResult([], 0) with
        {
            EffectiveDraft = forcedExactBase,
        };
        fixture.Controller.UpdateCurrentDraft(categoryDraft, ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(
            BaseSearchMode.ExactBase,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
    }

    [Fact]
    public void ModifierSelection_ImmediatelyUsesTheProviderEffectiveDraftAndRestoresTheUserCategory()
    {
        var fixture = SearchFixture.Create();
        var categoryDraft = DraftWithBothBaseCriteria("Belt", "Stygian Vise") with
        {
            ModifierFilters =
            [
                Modifier("Has 1 Abyssal Socket", ParsedModifierKind.Implicit) with
                {
                    IsBaseImplicit = true,
                },
            ],
        };
        fixture.PriceCheckService.EffectiveDraftResolver = draft =>
        {
            var activeCriterion = draft.ModifierFilters.Any(modifier => modifier.IsSelected && modifier.IsBaseImplicit)
                ? draft.Base.AvailableCriteria.ExactBase
                : draft.Base.AvailableCriteria.Category;
            return draft with
            {
                Base = draft.Base with
                {
                    ActiveCriterion = activeCriterion,
                },
            };
        };
        fixture.Controller.UpdateCurrentDraft(categoryDraft, ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Equal(
            BaseSearchMode.ExactBase,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);

        Assert.Equal(
            BaseSearchMode.Category,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
    }

    [Fact]
    public async Task PreparePresentationAsync_DelayedProviderMetadataReturnsTheOfficialLabelWithoutSearch()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.CategoryLabelLoader = (_, _) => completion.Task;
        var draft = DraftWithBothBaseCriteria("Wand", "Imbued Wand");

        var preparation = fixture.Controller.PreparePresentationAsync(
            draft,
            new PriceCheckerItemPresentation());

        await WaitUntilAsync(() => fixture.PriceCheckService.CategoryLabelLoadCalls.Count == 1);
        Assert.False(preparation.IsCompleted);
        Assert.Empty(fixture.PriceCheckService.Calls);

        completion.SetResult("Wand");
        var presentation = await preparation;

        Assert.Equal("Wand", presentation.CategoryDisplayLabel);
        Assert.Empty(fixture.PriceCheckService.Calls);
    }

    [Fact]
    public async Task PreparePresentationAsync_FailureLeavesTheLabelUnsetAndDoesNotBlockSearch()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.CategoryLabelLoader = (_, _) => Task.FromResult<string?>(null);
        var draft = DraftWithBothBaseCriteria();
        var presentation = await fixture.Controller.PreparePresentationAsync(
            draft,
            new PriceCheckerItemPresentation());
        fixture.Controller.UpdateCurrentDraft(draft, ValidationSuccess(), presentation);

        await fixture.Controller.SearchAsync();

        Assert.Null(fixture.Window.CurrentState?.Presentation.CategoryDisplayLabel);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
    }

    private static TradeSearchDraft Draft(
        string name,
        bool selectedModifier = false,
        IReadOnlyList<ResolvedSearchComponent>? modifiers = null)
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
            ModifierFilters = modifiers ?? (selectedModifier
                ? [Modifier("+10 to maximum Life", isSelected: true)]
                : []),
        };
    }

    private static TradeSearchDraft DraftWithBothBaseCriteria(
        string categoryName = "One Hand Axes",
        string exactBaseName = "Reaver Axe")
    {
        var category = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = categoryName,
        };
        var exactBase = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = category.Category,
            ExactBaseName = exactBaseName,
        };
        return Draft("Armageddon Thirst") with
        {
            ParsedBaseType = exactBaseName,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = $"base.{exactBaseName.ToLowerInvariant().Replace(' ', '-')}",
                ResolvedBaseName = exactBaseName,
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = category,
                    ExactBase = exactBase,
                },
                ActiveCriterion = category,
            },
        };
    }

    private static ResolvedSearchComponent Modifier(
        string originalText,
        ParsedModifierKind kind = ParsedModifierKind.Prefix,
        bool isSelected = false,
        string? resolvedModifierId = "mod.test")
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = originalText,
            CanonicalSignature = originalText,
            ParsedKind = kind,
            ResolutionStatus = resolvedModifierId is null
                ? ModifierCandidateResolutionStatus.Unknown
                : ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = resolvedModifierId,
            ResolvedStatIds = resolvedModifierId is null
                ? []
                : ["stat.test"],
            IsSearchable = resolvedModifierId is not null,
            IsSelected = isSelected,
        };
    }

    private static TradeSearchValidationResult ValidationSuccess()
    {
        return TradeSearchValidationResult.FromDiagnostics([]);
    }

    private static PathOfExileTradePriceCheckResult SuccessResult(
        IReadOnlyList<PathOfExileTradeFetchedOffer> offers,
        int total,
        bool? inexact = null,
        IReadOnlyList<string>? resultIds = null,
        IReadOnlyList<string>? fetchedResultIds = null)
    {
        return new PathOfExileTradePriceCheckResult
        {
            IsSuccess = true,
            Stage = PathOfExileTradePriceCheckStage.Completed,
            SearchQueryId = "query-1",
            ResultIds = resultIds ?? [],
            FetchedResultIds = fetchedResultIds ?? [],
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
        string? rawIndexed = null,
        string? itemName = "Armoured Shell",
        int? itemLevel = 85,
        DateTimeOffset? indexed = null)
    {
        return new PathOfExileTradeFetchedOffer
        {
            Id = id,
            Item = new PathOfExileTradeFetchedItem
            {
                Name = itemName,
                TypeLine = "Titan Plate",
                ItemLevel = itemLevel,
            },
            Listing = new PathOfExileTradeListing
            {
                RawIndexed = rawIndexed,
                Indexed = indexed,
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

    private static IReadOnlyList<PathOfExileTradeFetchedOffer> OffersFor(IEnumerable<string> ids)
    {
        return ids
            .Select(id => Offer(
                id,
                amount: decimal.Parse(id.AsSpan("id-".Length), CultureInfo.InvariantCulture)))
            .ToArray();
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

    private sealed record LoadMoreCall(
        string? SearchQueryId,
        IReadOnlyList<string?>? ResultIds,
        CancellationToken CancellationToken);

    private sealed record CategoryLabelLoadCall(
        TradeSearchDraft Draft,
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

        public List<LoadMoreCall> LoadMoreCalls { get; } = [];

        public Queue<PathOfExileTradePriceCheckResult> PendingLoadMoreResults { get; } = [];

        public PathOfExileTradePriceCheckResult Result { get; set; } =
            SuccessResult([], total: 0);

        public Func<PriceCheckCall, Task<PathOfExileTradePriceCheckResult>>? Handler { get; set; }

        public Func<LoadMoreCall, Task<PathOfExileTradePriceCheckResult>>? LoadMoreHandler { get; set; }

        public Func<TradeSearchDraft, TradeSearchDraft>? EffectiveDraftResolver { get; set; }

        public List<CategoryLabelLoadCall> CategoryLabelLoadCalls { get; } = [];

        public Func<TradeSearchDraft, CancellationToken, Task<string?>>? CategoryLabelLoader { get; set; }

        public TradeSearchDraft ResolveEffectiveDraft(TradeSearchDraft draft)
        {
            return EffectiveDraftResolver?.Invoke(draft) ?? draft;
        }

        public Task<PathOfExileTradeFilterCatalogProviderResult> InitializeFilterCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PathOfExileTradeFilterCatalogProviderResult());
        }

        public Task<string?> LoadCategoryDisplayLabelAsync(
            TradeSearchDraft draft,
            CancellationToken cancellationToken = default)
        {
            CategoryLabelLoadCalls.Add(new CategoryLabelLoadCall(draft, cancellationToken));
            return CategoryLabelLoader is null
                ? Task.FromResult<string?>(null)
                : CategoryLabelLoader(draft, cancellationToken);
        }

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

        public Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
            string? searchQueryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default)
        {
            var call = new LoadMoreCall(searchQueryId, resultIds, cancellationToken);
            LoadMoreCalls.Add(call);
            if (LoadMoreHandler is not null)
            {
                return LoadMoreHandler(call);
            }

            if (PendingLoadMoreResults.Count == 0)
            {
                throw new InvalidOperationException("No fake Load More result was configured.");
            }

            return Task.FromResult(PendingLoadMoreResults.Dequeue());
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

        public PriceCheckerPlacement? GetDisplayedPlacement()
        {
            return CurrentPlacement;
        }

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

        public void SetPinned(bool isPinned)
        {
            IsPinned = isPinned;
            PinStateChanged?.Invoke(this, isPinned);
        }

        public void RaiseSearchRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseLoadMoreRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            LoadMoreRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseModifierSelectionChanged(int modifierIndex, bool isSelected)
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            ModifierSelectionChanged?.Invoke(
                this,
                new PriceCheckerModifierSelectionChangedEventArgs(modifierIndex, isSelected));
        }

        public void RaiseBaseCriterionToggleRequested()
        {
            BaseCriterionToggleRequested?.Invoke(this, EventArgs.Empty);
        }
    }
#pragma warning restore CS0067
}

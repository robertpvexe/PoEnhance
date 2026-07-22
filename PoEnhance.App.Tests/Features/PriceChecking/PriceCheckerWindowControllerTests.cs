using System.Reflection;
using System.Runtime.InteropServices;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerWindowControllerTests
{
    private static long visibilitySequence;

    [Fact]
    public async Task ShowOrUpdateAsync_EmptyCatalogCachePublishesFirstAxePresentationOnlyAfterOfficialLabelLoads()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Mapper.DraftFactory = parsedItem =>
            CategoryDraft(parsedItem, "One Hand Axes", "Reaver Axe");
        fixture.PriceCheckService.CategoryDisplayLabelResolver = _ => "One-Handed Axe";
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.CategoryLabelLoader = (_, _) => completion.Task;

        var update = fixture.Controller.ShowOrUpdateAsync(
            Item("Armageddon Thirst", "Reaver Axe"),
            null,
            []);

        await WaitUntilAsync(() => fixture.PriceCheckService.CategoryLabelLoadCount == 1);
        Assert.Empty(fixture.WindowFactory.CreatedWindows);
        completion.SetResult("One-Handed Axe");

        var result = await update;
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.True(result.IsSuccess);
        Assert.Equal("One-Handed Axe", window.CurrentState?.Presentation.CategoryDisplayLabel);
        Assert.Equal(BaseSearchMode.Category, window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(0, fixture.PriceCheckService.CallCount);
    }

    [Theory]
    [InlineData("Wand", "Imbued Wand", "Wand")]
    [InlineData("Belt", "Stygian Vise", "Belt")]
    public async Task ShowOrUpdateAsync_FirstPresentationUsesOfficialProviderLabel(
        string category,
        string exactBaseName,
        string providerLabel)
    {
        using var fixture = ControllerFixture.Create();
        fixture.Mapper.DraftFactory = parsedItem =>
            CategoryDraft(parsedItem, category, exactBaseName);
        fixture.PriceCheckService.CategoryDisplayLabelResolver = _ => providerLabel;

        var result = await fixture.Controller.ShowOrUpdateAsync(
            Item("First Loop", exactBaseName),
            null,
            []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.True(result.IsSuccess);
        Assert.Equal(providerLabel, window.CurrentState?.Presentation.CategoryDisplayLabel);
        Assert.Equal(0, fixture.PriceCheckService.CallCount);
    }

    [Fact]
    public async Task ShowOrUpdateAsync_StaleCatalogCompletionCannotReplaceANewerItem()
    {
        using var fixture = ControllerFixture.Create();
        var category = "Wand";
        var exactBaseName = "Imbued Wand";
        fixture.Mapper.DraftFactory = parsedItem =>
            CategoryDraft(parsedItem, category, exactBaseName);
        var first = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completions = new Queue<TaskCompletionSource<string?>>([first, second]);
        fixture.PriceCheckService.CategoryLabelLoader = (_, _) => completions.Dequeue().Task;

        var firstUpdate = fixture.Controller.ShowOrUpdateAsync(
            Item("First Loop", exactBaseName),
            null,
            []);
        await WaitUntilAsync(() => fixture.PriceCheckService.CategoryLabelLoadCount == 1);
        category = "Belt";
        exactBaseName = "Stygian Vise";
        var secondUpdate = fixture.Controller.ShowOrUpdateAsync(
            Item("Second Loop", exactBaseName),
            null,
            []);
        await WaitUntilAsync(() => fixture.PriceCheckService.CategoryLabelLoadCount == 2);

        second.SetResult("Belt");
        Assert.True((await secondUpdate).IsSuccess);
        first.SetResult("Wand");
        Assert.False((await firstUpdate).IsSuccess);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal("Second Loop", window.CurrentState?.Draft.DisplayName);
        Assert.Equal("Belt", window.CurrentState?.Presentation.CategoryDisplayLabel);
    }

    [Fact]
    public void ShowOrUpdate_SecondItemReusesSameWindowInstance()
    {
        using var fixture = ControllerFixture.Create();
        var firstItem = Item("First Loop", "Gold Ring");
        var secondItem = Item("Second Loop", "Two-Stone Ring");

        fixture.Controller.ShowOrUpdate(firstItem, null, []);
        fixture.Controller.ShowOrUpdate(secondItem, null, []);

        Assert.Single(fixture.WindowFactory.CreatedWindows);
    }

    [Fact]
    public void ShowOrUpdate_SecondItemReplacesDisplayedDraft()
    {
        using var fixture = ControllerFixture.Create();

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal("Second Loop", window.CurrentState?.Draft.DisplayName);
        Assert.Equal("Two-Stone Ring", window.CurrentState?.Draft.ParsedBaseType);
    }

    [Fact]
    public void ShowOrUpdate_AfterWindowCloseCreatesFreshWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);

        firstWindow.Close();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.True(firstWindow.IsClosed);
        Assert.False(fixture.WindowFactory.CreatedWindows[1].IsClosed);
        Assert.Equal("Second Loop", fixture.WindowFactory.CreatedWindows[1].CurrentState?.Draft.DisplayName);
    }

    [Fact]
    public void ShowOrUpdate_ReusesCoreDraftMappingAndValidationAdapters()
    {
        using var fixture = ControllerFixture.Create();

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(2, fixture.Mapper.CallCount);
        Assert.Equal(2, fixture.Validator.CallCount);
    }

    [Fact]
    public void ShowOrUpdate_DoesNotInvokePriceCheckSearch()
    {
        using var fixture = ControllerFixture.Create();

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);

        Assert.Equal(0, fixture.PriceCheckService.CallCount);
    }

    [Fact]
    public void OfferClick_OpensPreviewWithExactSnapshotAndPerformsNoSearchOrFetch()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var snapshot = new OfferCardSnapshot
        {
            OfferId = "offer-1",
            Name = "Dusk Shell",
        };

        priceCheckerWindow.RaiseOfferClicked(snapshot);

        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        Assert.Same(snapshot, preview.CurrentSnapshot);
        Assert.Equal(1, preview.ShowCount);
        Assert.Equal(0, fixture.PriceCheckService.CallCount);
    }

    [Fact]
    public void OfferClick_SecondAndRepeatedClicksReplaceContentInOneReusablePreview()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var first = new OfferCardSnapshot { OfferId = "first" };
        var second = new OfferCardSnapshot { OfferId = "second" };

        priceCheckerWindow.RaiseOfferClicked(first);
        priceCheckerWindow.RaiseOfferClicked(second);
        priceCheckerWindow.RaiseOfferClicked(second);

        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        Assert.Same(second, preview.CurrentSnapshot);
        Assert.Equal(3, preview.ShowCount);
        Assert.Equal(3, preview.Placements.Count);
    }

    [Fact]
    public void PriceCheckerCloseAndNewCaptureClearUnpinnedPreview()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        priceCheckerWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "offer-1" });
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);

        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Iron Ring"), null, []);

        Assert.Null(preview.CurrentSnapshot);
        Assert.True(preview.HideCount >= 1);

        var currentWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        currentWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "offer-2" });
        currentWindow.Close();

        Assert.Null(preview.CurrentSnapshot);
        Assert.True(preview.HideCount >= 2);
    }

    [Fact]
    public void ResetLeagueChangeAndNewSearchClearUnpinnedPreview()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var previewSnapshot = new OfferCardSnapshot { OfferId = "offer" };

        priceCheckerWindow.RaiseOfferClicked(previewSnapshot);
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        priceCheckerWindow.RaiseResetItemRequested();
        Assert.Null(preview.CurrentSnapshot);

        priceCheckerWindow.RaiseOfferClicked(previewSnapshot);
        Assert.True(fixture.LeagueSetting.TrySave("Standard"));
        Assert.Null(preview.CurrentSnapshot);

        priceCheckerWindow.RaiseOfferClicked(previewSnapshot);
        priceCheckerWindow.RaiseSearchRequested();
        Assert.Null(preview.CurrentSnapshot);
        Assert.Equal(1, fixture.PriceCheckService.CallCount);
    }

    [Fact]
    public void ControllerCloseDisposesPreviewWithoutClosingApplicationFromPreviewX()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        priceCheckerWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "offer" });
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);

        preview.RaiseCloseRequested();

        Assert.Null(preview.CurrentSnapshot);
        Assert.False(preview.IsClosed);
        Assert.False(priceCheckerWindow.IsClosed);

        fixture.Controller.Close();

        Assert.True(preview.IsClosed);
        Assert.True(priceCheckerWindow.IsClosed);
    }

    [Fact]
    public void PinningCreatesIndependentCardClearsPreviewAndLaterOfferReusesPreview()
    {
        using var fixture = ControllerFixture.Create();
        fixture.ForegroundWindowDetector.IsOverlayContextActive = true;
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var first = new OfferCardSnapshot { OfferId = "first" };
        var second = new OfferCardSnapshot { OfferId = "second" };

        priceCheckerWindow.RaiseOfferClicked(first);
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        var previewPlacement = Assert.IsType<PriceCheckerPlacement>(preview.CurrentPlacement);
        preview.RaisePinRequested();

        var pinned = Assert.Single(fixture.PinnedWindowFactory.Windows);
        Assert.Same(first, pinned.CurrentSnapshot);
        Assert.Equal(previewPlacement, pinned.CurrentPlacement);
        Assert.True(pinned.IsVisible);
        Assert.True(pinned.LastShowSequence > 0);
        Assert.True(pinned.LastShowSequence < preview.LastHideSequence);
        Assert.Null(preview.CurrentSnapshot);
        Assert.Equal(1, preview.HideCount);

        priceCheckerWindow.RaiseOfferClicked(second);

        Assert.Same(second, preview.CurrentSnapshot);
        Assert.Same(first, pinned.CurrentSnapshot);
        Assert.Single(fixture.PinnedWindowFactory.Windows);
        Assert.Equal(0, fixture.PriceCheckService.CallCount);
    }

    [Fact]
    public void UnpinMovesExactPinnedSnapshotBackIntoReusablePreviewAtItsCurrentPlacement()
    {
        using var fixture = ControllerFixture.Create();
        fixture.ForegroundWindowDetector.IsOverlayContextActive = true;
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var snapshot = new OfferCardSnapshot { OfferId = "pinned", Name = "Dusk Shell" };
        var mapperCalls = fixture.Mapper.CallCount;
        var validatorCalls = fixture.Validator.CallCount;

        priceCheckerWindow.RaiseOfferClicked(snapshot);
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        preview.RaisePinRequested();
        var pinned = Assert.Single(fixture.PinnedWindowFactory.Windows);
        pinned.RaiseDragDelta(35, 20);
        var pinnedPlacement = Assert.IsType<PriceCheckerPlacement>(pinned.CurrentPlacement);

        priceCheckerWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "other" });
        Assert.Same(preview, Assert.Single(fixture.PreviewWindowFactory.Windows));
        pinned.RaiseUnpinRequested();

        Assert.True(pinned.IsClosed);
        Assert.Equal(0, fixture.PinnedController.Count);
        Assert.Same(snapshot, preview.CurrentSnapshot);
        Assert.Equal(pinnedPlacement, preview.CurrentPlacement);
        Assert.Equal(mapperCalls, fixture.Mapper.CallCount);
        Assert.Equal(validatorCalls, fixture.Validator.CallCount);
        Assert.Equal(0, fixture.PriceCheckService.CallCount);
        Assert.Equal(0, fixture.PriceCheckService.FetchCallCount);

        priceCheckerWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "next" });
        Assert.Equal("next", preview.CurrentSnapshot?.OfferId);
    }

    [Fact]
    public void UnpinAfterPriceCheckerClosesUsesPinnedSessionBoundsForReusablePreview()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var snapshot = new OfferCardSnapshot { OfferId = "pinned" };
        priceCheckerWindow.RaiseOfferClicked(snapshot);
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        preview.RaisePinRequested();
        var pinned = Assert.Single(fixture.PinnedWindowFactory.Windows);

        priceCheckerWindow.Close();
        pinned.RaiseUnpinRequested();

        Assert.Same(snapshot, preview.CurrentSnapshot);
        Assert.True(pinned.IsClosed);
        Assert.Equal(0, fixture.PinnedController.Count);
    }

    [Fact]
    public void PinningSameOfferIdAgainBringsExistingCardForwardAndClearsDuplicatePreview()
    {
        using var fixture = ControllerFixture.Create();
        fixture.ForegroundWindowDetector.IsOverlayContextActive = true;
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var snapshot = new OfferCardSnapshot { OfferId = "listing-1", Name = "Dusk Shell" };

        priceCheckerWindow.RaiseOfferClicked(snapshot);
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        preview.RaisePinRequested();
        var pinned = Assert.Single(fixture.PinnedWindowFactory.Windows);
        var initialShowCount = pinned.ShowCount;

        priceCheckerWindow.RaiseOfferClicked(snapshot with { Name = "Rendered differently" });
        preview.RaisePinRequested();

        Assert.Single(fixture.PinnedWindowFactory.Windows);
        Assert.Equal(1, fixture.PinnedController.Count);
        Assert.True(pinned.ShowCount > initialShowCount);
        Assert.Null(preview.CurrentSnapshot);
        Assert.False(pinned.IsClosed);
    }

    [Fact]
    public void SearchResetLeagueAndNewCaptureDoNotCloseOrMutatePinnedCard()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var snapshot = new OfferCardSnapshot { OfferId = "pinned" };
        priceCheckerWindow.RaiseOfferClicked(snapshot);
        Assert.Single(fixture.PreviewWindowFactory.Windows).RaisePinRequested();
        var pinned = Assert.Single(fixture.PinnedWindowFactory.Windows);

        priceCheckerWindow.RaiseSearchRequested();
        priceCheckerWindow.RaiseResetItemRequested();
        Assert.True(fixture.LeagueSetting.TrySave("Standard"));
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Iron Ring"), null, []);

        Assert.Same(snapshot, pinned.CurrentSnapshot);
        Assert.False(pinned.IsClosed);
        Assert.Equal(1, fixture.PinnedController.Count);
        Assert.Equal(1, fixture.PriceCheckService.CallCount);
    }

    [Fact]
    public void ClosingPriceCheckerDoesNotClosePinnedCard()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        priceCheckerWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "pinned" });
        Assert.Single(fixture.PreviewWindowFactory.Windows).RaisePinRequested();
        var pinned = Assert.Single(fixture.PinnedWindowFactory.Windows);

        priceCheckerWindow.Close();

        Assert.True(priceCheckerWindow.IsClosed);
        Assert.False(pinned.IsClosed);
        Assert.NotNull(pinned.CurrentSnapshot);
    }

    [Fact]
    public void ApplicationShutdownClosesPinnedAndUnpinnedWindowsExactlyOnce()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        priceCheckerWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "pinned" });
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        preview.RaisePinRequested();
        var pinned = Assert.Single(fixture.PinnedWindowFactory.Windows);
        priceCheckerWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "preview" });

        fixture.Controller.Close();
        fixture.Controller.Close();

        Assert.True(preview.IsClosed);
        Assert.True(pinned.IsClosed);
        Assert.Equal(1, pinned.CloseCount);
        Assert.True(priceCheckerWindow.IsClosed);
    }

    [Fact]
    public void FifthPinAttemptKeepsPreviewOpenShowsFeedbackAndDoesNotReplacePinnedCards()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);

        for (var index = 1; index <= 4; index++)
        {
            priceCheckerWindow.RaiseOfferClicked(
                new OfferCardSnapshot { OfferId = index.ToString() });
            Assert.Single(fixture.PreviewWindowFactory.Windows).RaisePinRequested();
        }

        var fifth = new OfferCardSnapshot { OfferId = "fifth" };
        priceCheckerWindow.RaiseOfferClicked(fifth);
        var preview = Assert.Single(fixture.PreviewWindowFactory.Windows);
        preview.RaisePinRequested();

        Assert.Same(fifth, preview.CurrentSnapshot);
        Assert.Equal(
            PinnedOfferCardSessionController.MaximumPinnedCardsFeedback,
            preview.PinFeedback);
        Assert.Equal(4, fixture.PinnedWindowFactory.Windows.Count);
        Assert.All(fixture.PinnedWindowFactory.Windows, pinned => Assert.False(pinned.IsClosed));

        fixture.PinnedWindowFactory.Windows[0].RaiseCloseRequested();
        preview.RaisePinRequested();

        Assert.Null(preview.CurrentSnapshot);
        Assert.Equal(5, fixture.PinnedWindowFactory.Windows.Count);
        Assert.Same(fifth, fixture.PinnedWindowFactory.Windows[^1].CurrentSnapshot);
    }

    [Fact]
    public void PinningAndDraggingPerformNoSearchFetchOrDraftWork()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var mapperCalls = fixture.Mapper.CallCount;
        var validatorCalls = fixture.Validator.CallCount;
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        priceCheckerWindow.RaiseOfferClicked(new OfferCardSnapshot { OfferId = "pinned" });

        Assert.Single(fixture.PreviewWindowFactory.Windows).RaisePinRequested();
        Assert.Single(fixture.PinnedWindowFactory.Windows).RaiseDragDelta(35, 20);

        Assert.Equal(mapperCalls, fixture.Mapper.CallCount);
        Assert.Equal(validatorCalls, fixture.Validator.CallCount);
        Assert.Equal(0, fixture.PriceCheckService.CallCount);
        Assert.Equal(0, fixture.PriceCheckService.FetchCallCount);
    }

    [Fact]
    public void PinnedOfferCardForegroundChangesPerformNoSearchFetchOrDraftWork()
    {
        using var fixture = ControllerFixture.Create();
        fixture.ForegroundWindowDetector.IsOverlayContextActive = true;
        fixture.Controller.UpdateGameOverlayContext(true);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var priceCheckerWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var snapshot = new OfferCardSnapshot { OfferId = "pinned" };
        priceCheckerWindow.RaiseOfferClicked(snapshot);
        Assert.Single(fixture.PreviewWindowFactory.Windows).RaisePinRequested();
        var pinned = Assert.Single(fixture.PinnedWindowFactory.Windows);
        var placement = pinned.CurrentPlacement;
        var showCount = pinned.ShowCount;
        var mapperCalls = fixture.Mapper.CallCount;
        var validatorCalls = fixture.Validator.CallCount;

        fixture.ForegroundWindowDetector.IsOverlayContextActive = false;
        fixture.Controller.UpdateGameOverlayContext(false);
        fixture.Controller.UpdateGameOverlayContext(false);
        fixture.ForegroundWindowDetector.IsOverlayContextActive = true;
        fixture.Controller.UpdateGameOverlayContext(true);

        Assert.Equal(0, pinned.HideCount);
        Assert.Equal(showCount, pinned.ShowCount);
        Assert.Same(snapshot, pinned.CurrentSnapshot);
        Assert.Equal(placement, pinned.CurrentPlacement);
        Assert.Equal(mapperCalls, fixture.Mapper.CallCount);
        Assert.Equal(validatorCalls, fixture.Validator.CallCount);
        Assert.Equal(0, fixture.PriceCheckService.CallCount);
        Assert.Equal(0, fixture.PriceCheckService.FetchCallCount);
    }

    [Fact]
    public void ModifierSelection_DoesNotInvokePriceCheckSearchOrChangePlacement()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var placement = window.CurrentPlacement;

        window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Equal(0, fixture.PriceCheckService.CallCount);
        Assert.Equal(placement, window.CurrentPlacement);
    }

    [Fact]
    public void ShowOrUpdate_UnavailablePathOfExileBoundsDoesNotThrowOrCreateWindow()
    {
        using var fixture = ControllerFixture.Create(boundsAvailable: false);

        var exception = Record.Exception(() =>
            fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []));

        Assert.Null(exception);
        Assert.Empty(fixture.WindowFactory.CreatedWindows);
    }

    [Fact]
    public void ShowOrUpdate_InitialNonActivatedShowDoesNotCloseWhenPathOfExileIsForeground()
    {
        using var fixture = ControllerFixture.Create();
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.False(window.IsClosed);
        Assert.Empty(fixture.DeferredActionScheduler.PendingActions);
        Assert.Equal(1, window.ShowCount);
    }

    [Fact]
    public void PanelInteractionThenDeactivationToPathOfExileClosesUnpinnedWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.RaisePanelInteraction();
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void SearchInteractionThenDeactivationToPathOfExileClosesUnpinnedWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.RaiseSearchRequested();
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void PanelActivationThenDeactivationToPathOfExileClosesUnpinnedWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.RaisePanelActivated();
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void PanelDeactivationToAnotherApplicationDoesNotCloseWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = false;

        window.RaisePanelInteraction();
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.False(window.IsClosed);
    }

    [Fact]
    public void PinnedPanelRemainsOpenAfterDeactivationToPathOfExile()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.SetPinned(true);
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.False(window.IsClosed);
        Assert.True(window.IsPinned);
    }

    [Fact]
    public void UnpinningRestoresAutoCloseAfterDeactivationToPathOfExile()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.SetPinned(true);
        window.SetPinned(false);
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void CloseButtonPathClosesPinnedPanelAndNextUpdateReopensUnpinned()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        firstWindow.SetPinned(true);

        firstWindow.Close();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.True(firstWindow.IsClosed);
        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.False(fixture.WindowFactory.CreatedWindows[1].IsPinned);
    }

    [Fact]
    public void EscapeClosePathClosesFocusedPinnedPanel()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.SetPinned(true);

        window.RaiseEscapeClose();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void AutoCloseClearsLiveWindowReferenceAndNextUpdateCreatesFreshWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        firstWindow.RaisePanelInteraction();
        firstWindow.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.True(firstWindow.IsClosed);
        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.Equal("Second Loop", fixture.WindowFactory.CreatedWindows[1].CurrentState?.Draft.DisplayName);
    }

    [Fact]
    public void RecreatedWindowAfterAutoCloseStartsUnpinned()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        firstWindow.SetPinned(true);
        firstWindow.SetPinned(false);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        firstWindow.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.False(fixture.WindowFactory.CreatedWindows[1].IsPinned);
    }

    [Fact]
    public void PinStateIsNotPersistedInPlacementStorage()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);

        window.SetPinned(true);
        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();

        var json = File.ReadAllText(fixture.PlacementStore.FilePath);
        Assert.DoesNotContain("pin", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pinned", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistedCorrectionIsReusedAfterAutoCloseAndReopen()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        firstWindow.RaiseHorizontalDragDelta(-25);
        firstWindow.RaiseHorizontalDragCompleted();
        var adjustedLeft = firstWindow.CurrentPlacement?.Left;
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        firstWindow.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.Equal(adjustedLeft, fixture.WindowFactory.CreatedWindows[1].CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_WhilePinnedReusesWindowAndReplacesDraft()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.SetPinned(true);

        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.True(window.IsPinned);
        Assert.Equal("Second Loop", window.CurrentState?.Draft.DisplayName);
    }

    [Fact]
    public void HorizontalDragCompletion_PersistsRelativeCorrection()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);

        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();

        var key = PriceCheckerPlacementKey.FromClientBounds(fixture.Bounds);
        var correction = fixture.PlacementStore.LoadHorizontalCorrection(key);
        Assert.Equal(
            window.CurrentPlacement?.Left - fixture.Calculator.CalculateAutomaticLeft(
                fixture.Bounds,
                window.CurrentPlacement?.Width ?? 0),
            correction);
    }

    [Fact]
    public void HorizontalResizeCompletion_PersistsPanelWidthWithoutInvokingSearch()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var originalRight = window.CurrentPlacement?.Right;

        window.RaiseHorizontalResizeDelta(-50);
        window.RaiseHorizontalResizeCompleted();

        var key = PriceCheckerPlacementKey.FromClientBounds(fixture.Bounds);
        Assert.Equal(508, window.CurrentPlacement?.Width);
        Assert.Equal(originalRight, window.CurrentPlacement?.Right);
        Assert.Equal(508, fixture.PlacementStore.LoadPanelWidth(key));
        Assert.Equal(0, fixture.PriceCheckService.CallCount);
    }

    [Fact]
    public void HorizontalResizeStarted_DoesNotMoveWindowOrReloadPlacement()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var displayedPlacement = new PriceCheckerPlacement(
            Left: 180,
            Top: fixture.Bounds.Top,
            Width: 430,
            Height: fixture.Bounds.Height);
        window.SetDisplayedPlacement(displayedPlacement);
        File.WriteAllText(fixture.PlacementStore.FilePath, """
            {
              "PanelWidths": {
                "ignored": 320
              }
            }
            """);
        var applyCount = window.ApplyPlacementCount;

        window.RaiseHorizontalResizeStarted();

        Assert.Equal(applyCount, window.ApplyPlacementCount);
        Assert.Equal(displayedPlacement, window.GetDisplayedPlacement());
    }

    [Fact]
    public void HorizontalResize_FirstDeltaUsesActualDisplayedBoundsWithoutJump()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.SetDisplayedPlacement(new PriceCheckerPlacement(
            Left: 180,
            Top: fixture.Bounds.Top,
            Width: 430,
            Height: fixture.Bounds.Height));

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizeDelta(0);
        fixture.DeferredActionScheduler.RunPending();

        Assert.Equal(100, window.CurrentPlacement?.Left);
        Assert.Equal(510, window.CurrentPlacement?.Width);
        Assert.Equal(610, window.CurrentPlacement?.Right);
    }

    [Fact]
    public void HorizontalResizeStartedSessionPreservesOneFixedRightEdgeAcrossCumulativeDeltas()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var originalRight = window.CurrentPlacement?.Right;

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizeDelta(-50);
        fixture.DeferredActionScheduler.RunPending();
        var afterFirstDelta = window.CurrentPlacement;
        window.RaiseHorizontalResizeDelta(20);
        fixture.DeferredActionScheduler.RunPending();
        var afterSecondDelta = window.CurrentPlacement;

        Assert.Equal(originalRight, afterFirstDelta?.Right);
        Assert.Equal(originalRight, afterSecondDelta?.Right);
        Assert.Equal(508, afterFirstDelta?.Width);
        Assert.Equal(508, afterSecondDelta?.Width);
        Assert.Equal(fixture.Bounds.Top, afterSecondDelta?.Top);
        Assert.Equal(fixture.Bounds.Height, afterSecondDelta?.Height);
    }

    [Fact]
    public void HorizontalResize_MultipleSmallDeltasMatchOneEquivalentLargeDelta()
    {
        using var smallDeltaFixture = ControllerFixture.Create();
        smallDeltaFixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var smallDeltaWindow = Assert.Single(smallDeltaFixture.WindowFactory.CreatedWindows);
        smallDeltaWindow.RaiseHorizontalResizeStarted();
        smallDeltaWindow.RaiseHorizontalResizeDelta(-10);
        smallDeltaWindow.RaiseHorizontalResizeDelta(-15);
        smallDeltaWindow.RaiseHorizontalResizeDelta(5);
        smallDeltaFixture.DeferredActionScheduler.RunPending();

        using var largeDeltaFixture = ControllerFixture.Create();
        largeDeltaFixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var largeDeltaWindow = Assert.Single(largeDeltaFixture.WindowFactory.CreatedWindows);
        largeDeltaWindow.RaiseHorizontalResizeStarted();
        largeDeltaWindow.RaiseHorizontalResizeDelta(-20);
        largeDeltaFixture.DeferredActionScheduler.RunPending();

        Assert.Equal(largeDeltaWindow.CurrentPlacement, smallDeltaWindow.CurrentPlacement);
        Assert.Equal(508, smallDeltaWindow.CurrentPlacement?.Width);
    }

    [Fact]
    public void HorizontalResize_CoalescesRapidPointerChangesAndAppliesOnlyLatestPosition()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var originalRight = window.CurrentPlacement?.Right;

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizePointerToOffset(-120);
        window.RaiseHorizontalResizePointerToOffset(-80);
        window.RaiseHorizontalResizePointerToOffset(-30);

        Assert.Equal(0, window.NativeBoundsApplyCount);
        fixture.DeferredActionScheduler.RunPending();

        Assert.Equal(1, window.NativeBoundsApplyCount);
        Assert.Equal(508, window.CurrentPlacement?.Width);
        Assert.Equal(originalRight, window.CurrentPlacement?.Right);
    }

    [Fact]
    public void HorizontalResize_UsesLatestPointerPositionRatherThanReplayingStaleSamples()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var originalLeft = window.CurrentPlacement?.Left;
        var originalRight = window.CurrentPlacement?.Right;

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizePointerToOffset(-140);
        window.RaiseHorizontalResizePointerToOffset(20);

        fixture.DeferredActionScheduler.RunPending();

        Assert.Equal(508, window.CurrentPlacement?.Width);
        Assert.Equal(fixture.Bounds.Left, window.CurrentPlacement?.Left);
        Assert.Equal(originalRight, window.CurrentPlacement?.Right);
        Assert.Equal(1, window.NativeBoundsApplyCount);
    }

    [Fact]
    public void HorizontalResize_AppliesLeftAndWidthTogetherWithTopAndHeightUnchanged()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var originalRight = window.CurrentPlacement?.Right;

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizeDelta(-50);
        fixture.DeferredActionScheduler.RunPending();

        var native = Assert.IsType<PriceCheckerNativeRectangle>(window.NativeBounds);
        Assert.Equal(50, native.Top);
        Assert.Equal(800, native.Height);
        Assert.Equal(originalRight, native.Right);
        Assert.Equal(1, window.NativeBoundsApplyCount);
    }

    [Fact]
    public void HorizontalResize_GrabOffsetPreventsJumpAtResizeStart()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var originalLeft = window.CurrentPlacement?.Left;
        var originalRight = window.CurrentPlacement?.Right;

        window.RaiseHorizontalResizeStartedAtOffset(12);
        window.RaiseHorizontalResizePointerToOffset(12);
        fixture.DeferredActionScheduler.RunPending();

        Assert.Equal(fixture.Bounds.Left, window.CurrentPlacement?.Left);
        Assert.Equal(508, window.CurrentPlacement?.Width);
        Assert.Equal(originalRight, window.CurrentPlacement?.Right);
    }

    [Fact]
    public void HorizontalResizeCompletion_AppliesFinalPointerPositionAndDiscardsScheduledStaleUpdate()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizePointerToOffset(-70);
        window.RaiseHorizontalResizeCompleted();
        var applyCountAfterCompletion = window.NativeBoundsApplyCount;
        fixture.DeferredActionScheduler.RunPending();

        Assert.Equal(508, window.CurrentPlacement?.Width);
        Assert.Equal(applyCountAfterCompletion, window.NativeBoundsApplyCount);
        Assert.Equal(1, applyCountAfterCompletion);
    }

    [Fact]
    public void HorizontalResizeLostCapture_AppliesFinalPointerAndCleansUpScheduledUpdate()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizePointerToOffset(-50);
        window.RaiseHorizontalResizeLostCapture();
        var applyCountAfterLostCapture = window.NativeBoundsApplyCount;
        fixture.DeferredActionScheduler.RunPending();

        Assert.Equal(508, window.CurrentPlacement?.Width);
        Assert.Equal(applyCountAfterLostCapture, window.NativeBoundsApplyCount);
        Assert.Equal(1, applyCountAfterLostCapture);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void HorizontalResize_UsesDipDeltasConsistentlyAcrossDpi(double dpiScale)
    {
        var bounds = new PathOfExileClientBounds(
            Left: 100,
            Top: 50,
            Width: 1000,
            Height: 800,
            DisplayDeviceName: @"\\.\DISPLAY1",
            DpiScaleX: dpiScale,
            DpiScaleY: dpiScale);
        using var fixture = ControllerFixture.Create(bounds: bounds);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var originalRight = window.CurrentPlacement?.Right;

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizeDelta(-50);
        fixture.DeferredActionScheduler.RunPending();

        Assert.Equal(508, window.CurrentPlacement?.Width);
        Assert.Equal(originalRight, window.CurrentPlacement?.Right);
    }

    [Fact]
    public void HorizontalResize_MaximumWidthReachesClientLeftMarginAndKeepsRightFixed()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        var originalRight = Assert.IsType<double>(window.CurrentPlacement?.Right);

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizeDelta(-1000);
        fixture.DeferredActionScheduler.RunPending();

        Assert.Equal(originalRight, window.CurrentPlacement?.Right);
        Assert.Equal(508, window.CurrentPlacement?.Width);
        Assert.Equal(fixture.Bounds.Left, window.CurrentPlacement?.Left);
    }

    [Fact]
    public void HorizontalResizeDelta_DoesNotWritePlacementStore()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizeDelta(-50);
        fixture.DeferredActionScheduler.RunPending();

        Assert.False(File.Exists(fixture.PlacementStore.FilePath));
    }

    [Fact]
    public void HorizontalResizeCompletion_PreservesExistingHorizontalCorrection()
    {
        using var fixture = ControllerFixture.Create();
        var key = PriceCheckerPlacementKey.FromClientBounds(fixture.Bounds);
        fixture.PlacementStore.SaveHorizontalCorrection(key, -20);
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.SetDisplayedPlacement(new PriceCheckerPlacement(
            Left: 210,
            Top: fixture.Bounds.Top,
            Width: 430,
            Height: fixture.Bounds.Height));

        window.RaiseHorizontalResizeStarted();
        window.RaiseHorizontalResizeDelta(-10);
        window.RaiseHorizontalResizeCompleted();

        Assert.Equal(-20, fixture.PlacementStore.LoadHorizontalCorrection(key));
        Assert.Equal(540, fixture.PlacementStore.LoadPanelWidth(key));
    }

    [Fact]
    public void PersistedWidthIsReusedAfterAutoCloseAndReopen()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        firstWindow.RaiseHorizontalResizeDelta(-50);
        firstWindow.RaiseHorizontalResizeCompleted();
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        firstWindow.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.Equal(500, fixture.WindowFactory.CreatedWindows[1].CurrentPlacement?.Width);
    }

    [Fact]
    public void ShowOrUpdate_AfterDragPreservesAdjustedXForSamePlacementKey()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();
        var adjustedLeft = window.CurrentPlacement?.Left;

        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal(adjustedLeft, window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_AfterResizePreservesWidthForSamePlacementKey()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalResizeDelta(-50);
        window.RaiseHorizontalResizeCompleted();

        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal(500, window.CurrentPlacement?.Width);
    }

    [Fact]
    public void ShowOrUpdate_FailedStoreWriteStillPreservesCorrectionForSession()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        File.WriteAllText(fixture.PlacementStore.FilePath, "{not valid json");

        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();
        var adjustedLeft = window.CurrentPlacement?.Left;
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(adjustedLeft, window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_FailedStoreWriteStillPreservesWidthForSession()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        File.WriteAllText(fixture.PlacementStore.FilePath, "{not valid json");

        window.RaiseHorizontalResizeDelta(-50);
        window.RaiseHorizontalResizeCompleted();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(500, window.CurrentPlacement?.Width);
    }

    [Fact]
    public void ShowOrUpdate_WhenPlacementKeyChangesLoadsCorrectionForNewKey()
    {
        using var fixture = ControllerFixture.Create();
        var secondBounds = fixture.Bounds with { Width = 1200 };
        var secondKey = PriceCheckerPlacementKey.FromClientBounds(secondBounds);
        fixture.PlacementStore.SaveHorizontalCorrection(secondKey, -30);

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalDragDelta(-60);
        window.RaiseHorizontalDragCompleted();

        fixture.BoundsProvider.Bounds = secondBounds;
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(
            fixture.Calculator.CalculatePlacement(secondBounds, -30).Left,
            window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_WhenPlacementKeyChangesLoadsWidthForNewKey()
    {
        using var fixture = ControllerFixture.Create();
        var secondBounds = fixture.Bounds with { Width = 1200 };
        var secondKey = PriceCheckerPlacementKey.FromClientBounds(secondBounds);
        fixture.PlacementStore.SavePanelWidth(secondKey, 420);

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalResizeDelta(-50);
        window.RaiseHorizontalResizeCompleted();

        fixture.BoundsProvider.Bounds = secondBounds;
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(576, window.CurrentPlacement?.Width);
        Assert.Equal(
            fixture.Calculator.CalculatePlacement(secondBounds, 0, 420).Left,
            window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_WhenPlacementKeyChangesUsesResponsiveDefaultWithoutSavedWidth()
    {
        using var fixture = ControllerFixture.Create();
        var secondBounds = fixture.Bounds with { Width = 1200 };

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalResizeDelta(-50);
        window.RaiseHorizontalResizeCompleted();

        fixture.BoundsProvider.Bounds = secondBounds;
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(576, window.CurrentPlacement?.Width);
    }

    [Fact]
    public void ShowOrUpdate_WhenPlacementKeyChangesUsesZeroWithoutSavedCorrection()
    {
        using var fixture = ControllerFixture.Create();
        var secondBounds = fixture.Bounds with { Width = 1200 };

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalDragDelta(-60);
        window.RaiseHorizontalDragCompleted();

        fixture.BoundsProvider.Bounds = secondBounds;
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(
            fixture.Calculator.CalculateAutomaticLeft(secondBounds),
            window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_ExactBaseResolutionIsDisplayedAsExact()
    {
        using var fixture = ControllerFixture.Create();

        fixture.Controller.ShowOrUpdate(
            Item("Armoured Shell", "Titan Plate"),
            ExactBase("base.titan-plate", "Titan Plate"),
            []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal(ItemBaseResolutionStatus.Exact, window.CurrentState?.Draft.Base.Status);
        Assert.Equal("Titan Plate", window.CurrentState?.Draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void ShowOrUpdate_ReplacesParserOnlyBaseStateWithExactBaseState()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("Armoured Shell", "Titan Plate"), null, []);

        fixture.Controller.ShowOrUpdate(
            Item("Armoured Shell", "Titan Plate"),
            ExactBase("base.titan-plate", "Titan Plate"),
            []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal(ItemBaseResolutionStatus.Exact, window.CurrentState?.Draft.Base.Status);
        Assert.Equal("Titan Plate", window.CurrentState?.Draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void ShowOrUpdate_ReplacesExactBaseStateWithParserOnlyBaseState()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(
            Item("Armoured Shell", "Titan Plate"),
            ExactBase("base.titan-plate", "Titan Plate"),
            []);

        fixture.Controller.ShowOrUpdate(Item("Armoured Shell", "Titan Plate"), null, []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Null(window.CurrentState?.Draft.Base.Status);
        Assert.Null(window.CurrentState?.Draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void CoreAssembly_GainsNoWpfWin32FileStorageOrNetworkingDependency()
    {
        var referencedNames = typeof(TradeSearchDraft).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("PresentationCore", referencedNames);
        Assert.DoesNotContain("PresentationFramework", referencedNames);
        Assert.DoesNotContain("WindowsBase", referencedNames);
        Assert.DoesNotContain("System.Net.Http", referencedNames);
        Assert.DoesNotContain("System.IO.FileSystem", referencedNames);
    }

    [Fact]
    public void PriceCheckerUi_DoesNotInvokeTradeSearchOrFetchClients()
    {
        var priceCheckerTypes = typeof(PriceCheckerWindowController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Features.PriceChecking")
            .ToArray();

        Assert.DoesNotContain(priceCheckerTypes, type =>
            Contains(type, "PathOfExileTradeSearchClient") ||
            Contains(type, "PathOfExileTradeFetchClient"));
        Assert.DoesNotContain(priceCheckerTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradeSearchClient") ||
            Contains(type, "PathOfExileTradeFetchClient"));
    }

    [Fact]
    public void AppAssembly_DoesNotIntroduceGlobalMouseOrKeyboardHooks()
    {
        var importedFunctionNames = ImportedFunctionNames();

        Assert.DoesNotContain("SetWindowsHookExA", importedFunctionNames);
        Assert.DoesNotContain("SetWindowsHookExW", importedFunctionNames);
        Assert.DoesNotContain("SetWindowsHookEx", importedFunctionNames);
        Assert.DoesNotContain("CallNextHookEx", importedFunctionNames);
        Assert.DoesNotContain("UnhookWindowsHookEx", importedFunctionNames);
        Assert.DoesNotContain("RegisterRawInputDevices", importedFunctionNames);
        Assert.DoesNotContain("GetRawInputData", importedFunctionNames);
    }

    [Fact]
    public void AppAssembly_DoesNotIntroduceExclusiveFullscreenOverlayDependencies()
    {
        var referencedNames = typeof(PriceCheckerWindowController).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var importedFunctionNames = ImportedFunctionNames();

        Assert.DoesNotContain("SharpDX", referencedNames);
        Assert.DoesNotContain("Vortice.Direct3D11", referencedNames);
        Assert.DoesNotContain("Vortice.DXGI", referencedNames);
        Assert.DoesNotContain("SlimDX", referencedNames);
        Assert.DoesNotContain("Silk.NET.Direct3D11", referencedNames);
        Assert.DoesNotContain("Direct3DCreate9", importedFunctionNames);
        Assert.DoesNotContain("D3D11CreateDevice", importedFunctionNames);
    }

    private static ParsedItem Item(string name, string baseType)
    {
        return new ItemTextParser().Parse($"""
Item Class: Rings
Rarity: Rare
{name}
{baseType}
--------
Item Level: 80
""");
    }

    private static TradeSearchDraft CategoryDraft(
        ParsedItem parsedItem,
        string category,
        string exactBaseName)
    {
        var categoryCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = category,
        };
        return new TradeSearchDraft
        {
            ItemClass = parsedItem.ItemClass,
            Rarity = parsedItem.Rarity,
            DisplayName = parsedItem.DisplayName,
            ParsedBaseType = exactBaseName,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseName = exactBaseName,
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = categoryCriterion,
                    ExactBase = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.ExactBase,
                        Category = category,
                        ExactBaseName = exactBaseName,
                    },
                },
                ActiveCriterion = categoryCriterion,
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

    private static HashSet<string> ImportedFunctionNames()
    {
        return typeof(PriceCheckerWindowController).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(method => new
            {
                MethodName = method.Name,
                Attribute = method.GetCustomAttribute<DllImportAttribute>(),
            })
            .Where(import => import.Attribute is not null)
            .Select(import => import.Attribute?.EntryPoint ?? import.MethodName)
            .Where(entryPoint => !string.IsNullOrWhiteSpace(entryPoint))
            .Select(entryPoint => entryPoint!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Type> ReferencedMemberTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        return type.GetConstructors(flags).SelectMany(ConstructorTypes)
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
            .Concat(type.GetMethods(flags).SelectMany(method =>
                method.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    private static IEnumerable<Type> ConstructorTypes(ConstructorInfo constructor)
    {
        return constructor.GetParameters().Select(parameter => parameter.ParameterType);
    }

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static ItemBaseResolutionResult ExactBase(string id, string name)
    {
        return new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = new ItemBaseRecord { Id = id, Name = name },
            ResolvedBaseId = id,
            ResolvedBaseName = name,
        };
    }

    private sealed class ControllerFixture : IDisposable
    {
        private readonly TempDirectory tempDirectory;

        private ControllerFixture(
            TempDirectory tempDirectory,
            PriceCheckerWindowController controller,
            PathOfExileClientBounds bounds,
            FakeBoundsProvider boundsProvider,
            PriceCheckerPlacementCalculator calculator,
            PriceCheckerPlacementStore placementStore,
            FakeWindowFactory windowFactory,
            CountingMapper mapper,
            CountingValidator validator,
            FakePriceCheckService priceCheckService,
            FakeForegroundWindowDetector foregroundWindowDetector,
            FakeDeferredActionScheduler deferredActionScheduler,
            ApplicationLeagueSetting leagueSetting,
            FakePreviewWindowFactory previewWindowFactory,
            OfferCardPreviewController previewController,
            FakePinnedWindowFactory pinnedWindowFactory,
            PinnedOfferCardSessionController pinnedController)
        {
            this.tempDirectory = tempDirectory;
            Controller = controller;
            Bounds = bounds;
            BoundsProvider = boundsProvider;
            Calculator = calculator;
            PlacementStore = placementStore;
            WindowFactory = windowFactory;
            Mapper = mapper;
            Validator = validator;
            PriceCheckService = priceCheckService;
            ForegroundWindowDetector = foregroundWindowDetector;
            DeferredActionScheduler = deferredActionScheduler;
            LeagueSetting = leagueSetting;
            PreviewWindowFactory = previewWindowFactory;
            PreviewController = previewController;
            PinnedWindowFactory = pinnedWindowFactory;
            PinnedController = pinnedController;
        }

        public PriceCheckerWindowController Controller { get; }

        public PathOfExileClientBounds Bounds { get; }

        public FakeBoundsProvider BoundsProvider { get; }

        public PriceCheckerPlacementCalculator Calculator { get; }

        public PriceCheckerPlacementStore PlacementStore { get; }

        public FakeWindowFactory WindowFactory { get; }

        public CountingMapper Mapper { get; }

        public CountingValidator Validator { get; }

        public FakePriceCheckService PriceCheckService { get; }

        public FakeForegroundWindowDetector ForegroundWindowDetector { get; }

        public FakeDeferredActionScheduler DeferredActionScheduler { get; }

        public ApplicationLeagueSetting LeagueSetting { get; }

        public FakePreviewWindowFactory PreviewWindowFactory { get; }

        public OfferCardPreviewController PreviewController { get; }

        public FakePinnedWindowFactory PinnedWindowFactory { get; }

        public PinnedOfferCardSessionController PinnedController { get; }

        public static ControllerFixture Create(
            bool boundsAvailable = true,
            PathOfExileClientBounds? bounds = null)
        {
            var tempDirectory = TempDirectory.Create();
            var clientBounds = bounds ?? new PathOfExileClientBounds(
                Left: 100,
                Top: 50,
                Width: 1000,
                Height: 800,
                DisplayDeviceName: @"\\.\DISPLAY1",
                DpiScaleX: 1,
                DpiScaleY: 1);
            var boundsProvider = new FakeBoundsProvider(boundsAvailable, clientBounds);
            var calculator = new PriceCheckerPlacementCalculator();
            var placementStore = new PriceCheckerPlacementStore(
                Path.Combine(tempDirectory.Path, "placement.json"));
            var windowFactory = new FakeWindowFactory(clientBounds.DpiScaleX, clientBounds.DpiScaleY);
            var mapper = new CountingMapper();
            var validator = new CountingValidator();
            var priceCheckService = new FakePriceCheckService();
            var foregroundWindowDetector = new FakeForegroundWindowDetector();
            var deferredActionScheduler = new FakeDeferredActionScheduler();
            var leagueSetting = ApplicationLeagueSetting.CreateTransient("Mirage");
            var previewWindowFactory = new FakePreviewWindowFactory();
            var previewController = new OfferCardPreviewController(
                previewWindowFactory,
                new OfferCardPreviewPlacementCalculator());
            var pinnedWindowFactory = new FakePinnedWindowFactory();
            var pinnedController = new PinnedOfferCardSessionController(
                pinnedWindowFactory,
                new PinnedOfferCardPlacementCalculator());
            var controller = new PriceCheckerWindowController(
                boundsProvider,
                calculator,
                placementStore,
                windowFactory,
                mapper,
                validator,
                foregroundWindowDetector,
                deferredActionScheduler,
                new PriceCheckerSearchController(
                    priceCheckService,
                    leagueSetting),
                offerCardPreviewController: previewController,
                pinnedOfferCardSessionController: pinnedController);

            return new ControllerFixture(
                tempDirectory,
                controller,
                clientBounds,
                boundsProvider,
                calculator,
                placementStore,
                windowFactory,
                mapper,
                validator,
                priceCheckService,
                foregroundWindowDetector,
                deferredActionScheduler,
                leagueSetting,
                previewWindowFactory,
                previewController,
                pinnedWindowFactory,
                pinnedController);
        }

        public void Dispose()
        {
            tempDirectory.Dispose();
        }
    }

    private sealed class FakeBoundsProvider : IPathOfExileClientBoundsProvider
    {
        private readonly bool isAvailable;

        public FakeBoundsProvider(bool isAvailable, PathOfExileClientBounds bounds)
        {
            this.isAvailable = isAvailable;
            Bounds = bounds;
        }

        public PathOfExileClientBounds Bounds { get; set; }

        public bool TryGetClientBounds(out PathOfExileClientBounds clientBounds)
        {
            clientBounds = Bounds;
            return isAvailable;
        }
    }

    private sealed class FakeForegroundWindowDetector : IPathOfExileForegroundWindowDetector
    {
        public bool IsPathOfExileForeground { get; set; }

        public bool IsOverlayContextActive { get; set; }

        public bool IsPathOfExileForegroundWindow()
        {
            return IsPathOfExileForeground;
        }

        public bool IsPathOfExileOverlayContextActive()
        {
            return IsOverlayContextActive || IsPathOfExileForeground;
        }
    }

    private sealed class FakeDeferredActionScheduler : IPriceCheckerDeferredActionScheduler
    {
        public List<Action> PendingActions { get; } = [];

        public void Schedule(Action action)
        {
            PendingActions.Add(action);
        }

        public void RunPending()
        {
            var actions = PendingActions.ToArray();
            PendingActions.Clear();
            foreach (var action in actions)
            {
                action();
            }
        }
    }

    internal sealed class FakePreviewWindowFactory : IOfferCardPreviewWindowFactory
    {
        public List<FakePreviewWindow> Windows { get; } = [];

        public IOfferCardPreviewWindow CreateWindow()
        {
            var window = new FakePreviewWindow();
            Windows.Add(window);
            return window;
        }
    }

    internal sealed class FakePreviewWindow : IOfferCardPreviewWindow
    {
        public event EventHandler? CloseRequested;

        public event EventHandler? PinRequested;

        public bool IsClosed { get; private set; }

        public OfferCardSnapshot? CurrentSnapshot { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public string? PinFeedback { get; private set; }

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public bool IsVisible { get; private set; }

        public long LastHideSequence { get; private set; }

        public List<PriceCheckerPlacement> Placements { get; } = [];

        public OfferCardPreviewSize UpdateContent(
            OfferCardSnapshot snapshot,
            double maximumWidth,
            double maximumHeight)
        {
            CurrentSnapshot = snapshot;
            return new OfferCardPreviewSize(Math.Min(600, maximumWidth), Math.Min(600, maximumHeight));
        }

        public void ApplyPlacement(PriceCheckerPlacement placement)
        {
            Placements.Add(placement);
            CurrentPlacement = placement;
        }

        public void ShowInactive()
        {
            ShowCount++;
            IsVisible = true;
        }

        public void HideAndClear()
        {
            CurrentSnapshot = null;
            CurrentPlacement = null;
            HideCount++;
            IsVisible = false;
            LastHideSequence = Interlocked.Increment(ref visibilitySequence);
        }

        public void SetPinFeedback(string? message)
        {
            PinFeedback = message;
        }

        public void Close()
        {
            CurrentSnapshot = null;
            IsClosed = true;
        }

        public void RaiseCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaisePinRequested()
        {
            PinRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    internal sealed class FakePinnedWindowFactory : IPinnedOfferCardWindowFactory
    {
        public List<FakePinnedWindow> Windows { get; } = [];

        public IPinnedOfferCardWindow CreateWindow()
        {
            var window = new FakePinnedWindow();
            Windows.Add(window);
            return window;
        }
    }

    internal sealed class FakePinnedWindow : IPinnedOfferCardWindow
    {
        public event EventHandler? CloseRequested;

        public event EventHandler? UnpinRequested;

        public event EventHandler<OfferCardDragDeltaEventArgs>? DragDelta;

        public bool IsClosed { get; private set; }

        public OfferCardSnapshot? CurrentSnapshot { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public int CloseCount { get; private set; }

        public bool IsVisible { get; private set; }

        public long LastShowSequence { get; private set; }

        public OfferCardPreviewSize UpdateContent(
            OfferCardSnapshot snapshot,
            double maximumWidth,
            double maximumHeight)
        {
            CurrentSnapshot = snapshot;
            return new OfferCardPreviewSize(Math.Min(600, maximumWidth), Math.Min(600, maximumHeight));
        }

        public void ApplyPlacement(PriceCheckerPlacement placement)
        {
            CurrentPlacement = placement;
        }

        public void ShowInactive()
        {
            ShowCount++;
            IsVisible = true;
            LastShowSequence = Interlocked.Increment(ref visibilitySequence);
        }

        public void Close()
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            CloseCount++;
        }

        public void RaiseCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseUnpinRequested()
        {
            UnpinRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseDragDelta(double horizontalChange, double verticalChange)
        {
            DragDelta?.Invoke(
                this,
                new OfferCardDragDeltaEventArgs(horizontalChange, verticalChange));
        }
    }

    private sealed class FakeWindowFactory : IPriceCheckerWindowFactory
    {
        private readonly double dpiScaleX;
        private readonly double dpiScaleY;

        public FakeWindowFactory(double dpiScaleX, double dpiScaleY)
        {
            this.dpiScaleX = dpiScaleX;
            this.dpiScaleY = dpiScaleY;
        }

        public List<FakeWindow> CreatedWindows { get; } = [];

        public IPriceCheckerWindow CreateWindow()
        {
            var window = new FakeWindow(dpiScaleX, dpiScaleY);
            CreatedWindows.Add(window);
            return window;
        }
    }

    private sealed class FakeWindow : IPriceCheckerWindow, IPriceCheckerNativeResizeWindow
    {
        private readonly double dpiScaleX;
        private readonly double dpiScaleY;

        public FakeWindow(double dpiScaleX, double dpiScaleY)
        {
            this.dpiScaleX = dpiScaleX;
            this.dpiScaleY = dpiScaleY;
        }

        public event EventHandler? Closed;

        public event EventHandler? PanelActivated;

        public event EventHandler? PanelDeactivated;

        public event EventHandler? PanelInteraction;

        public event EventHandler? SearchRequested;

        public event EventHandler? LoadMoreRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? TradeRequested
        {
            add { }
            remove { }
        }

        public event EventHandler<PriceCheckerOfferClickedEventArgs>? OfferClicked;

        public event EventHandler<PriceCheckerOfferCapacityChangedEventArgs>? OfferCapacityChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PriceCheckerItemPropertySelectionChangedEventArgs>? ItemPropertySelectionChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PriceCheckerItemPropertyBoundsChangedEventArgs>? ItemPropertyBoundsChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PriceCheckerItemPropertyExpansionChangedEventArgs>? ItemPropertyExpansionChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;

        public event EventHandler<PriceCheckerModifierBoundsChangedEventArgs>? ModifierBoundsChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PriceCheckerModifierFilterVariantChangedEventArgs>? ModifierFilterVariantChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PriceCheckerModifierExpansionChangedEventArgs>? ModifierExpansionChanged
        {
            add { }
            remove { }
        }

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

        public int ShowCount { get; private set; }

        public int ApplyPlacementCount { get; private set; }

        public int NativeBoundsApplyCount { get; private set; }

        public double CursorScreenX { get; private set; }

        public PriceCheckerNativeRectangle? NativeBounds { get; private set; }

        private bool IsHorizontalResizeActive { get; set; }

        private PriceCheckerPlacement? DisplayedPlacement { get; set; }

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
            ApplyPlacementCount++;
            CurrentPlacement = placement;
            DisplayedPlacement = placement;
            NativeBounds = ToNative(placement);
            CursorScreenX = NativeBounds.Left;
        }

        public PriceCheckerPlacement? GetDisplayedPlacement()
        {
            return DisplayedPlacement ?? CurrentPlacement;
        }

        public void SetDisplayedPlacement(PriceCheckerPlacement placement)
        {
            DisplayedPlacement = placement;
            NativeBounds = ToNative(placement);
            CursorScreenX = NativeBounds.Left;
        }

        public void ShowInactive()
        {
            ShowCount++;
        }

        public void RaiseHorizontalDragDelta(double horizontalChange)
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            HorizontalDragDelta?.Invoke(
                this,
                new PriceCheckerHorizontalDragEventArgs(horizontalChange));
        }

        public void RaiseHorizontalDragCompleted()
        {
            HorizontalDragCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseHorizontalResizeDelta(double horizontalChange)
        {
            if (!IsHorizontalResizeActive)
            {
                RaiseHorizontalResizeStarted();
            }

            PanelInteraction?.Invoke(this, EventArgs.Empty);
            CursorScreenX += horizontalChange * dpiScaleX;
            HorizontalResizeDelta?.Invoke(
                this,
                new PriceCheckerHorizontalResizeEventArgs(horizontalChange, CursorScreenX));
        }

        public void RaiseHorizontalResizePointerToOffset(double offsetFromLeft)
        {
            if (!IsHorizontalResizeActive)
            {
                RaiseHorizontalResizeStarted();
            }

            var native = Assert.IsType<PriceCheckerNativeRectangle>(NativeBounds);
            CursorScreenX = native.Left + (offsetFromLeft * dpiScaleX);
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            HorizontalResizeDelta?.Invoke(
                this,
                new PriceCheckerHorizontalResizeEventArgs(0, CursorScreenX));
        }

        public void RaiseHorizontalResizeStarted()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            if (NativeBounds is not null)
            {
                CursorScreenX = NativeBounds.Left;
            }

            IsHorizontalResizeActive = true;
            HorizontalResizeStarted?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseHorizontalResizeStartedAtOffset(double offsetFromLeft)
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            if (NativeBounds is not null)
            {
                CursorScreenX = NativeBounds.Left + (offsetFromLeft * dpiScaleX);
            }

            IsHorizontalResizeActive = true;
            HorizontalResizeStarted?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseHorizontalResizeCompleted()
        {
            IsHorizontalResizeActive = false;
            HorizontalResizeCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseHorizontalResizeLostCapture()
        {
            IsHorizontalResizeActive = false;
            HorizontalResizeCompleted?.Invoke(this, EventArgs.Empty);
        }

        public bool TryGetNativeBounds(out PriceCheckerNativeRectangle bounds)
        {
            bounds = NativeBounds!;
            return NativeBounds is not null;
        }

        public bool TryGetCursorScreenX(out double screenX)
        {
            screenX = CursorScreenX;
            return true;
        }

        public bool TrySetNativeBounds(PriceCheckerNativeRectangle bounds)
        {
            NativeBoundsApplyCount++;
            NativeBounds = bounds;
            CurrentPlacement = new PriceCheckerPlacement(
                bounds.Left / dpiScaleX,
                bounds.Top / dpiScaleY,
                bounds.Width / dpiScaleX,
                bounds.Height / dpiScaleY);
            DisplayedPlacement = CurrentPlacement;
            return true;
        }

        public void RaiseResetItemRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            ResetItemRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaisePanelActivated()
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
        }

        public void RaisePanelDeactivated()
        {
            PanelDeactivated?.Invoke(this, EventArgs.Empty);
        }

        public void RaisePanelInteraction()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseSearchRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseOfferClicked(OfferCardSnapshot snapshot)
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            OfferClicked?.Invoke(this, new PriceCheckerOfferClickedEventArgs(snapshot));
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

        public void SetPinned(bool isPinned)
        {
            IsPinned = isPinned;
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            PinStateChanged?.Invoke(this, isPinned);
        }

        public void RaiseEscapeClose()
        {
            Close();
        }

        public void Close()
        {
            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        private PriceCheckerNativeRectangle ToNative(PriceCheckerPlacement placement)
        {
            return new PriceCheckerNativeRectangle(
                placement.Left * dpiScaleX,
                placement.Top * dpiScaleY,
                placement.Width * dpiScaleX,
                placement.Height * dpiScaleY);
        }
    }

    private sealed class CountingMapper : ITradeSearchDraftMapper
    {
        public int CallCount { get; private set; }

        public Func<ParsedItem, TradeSearchDraft>? DraftFactory { get; set; }

        public TradeSearchDraftResult CreateDraft(
            ParsedItem parsedItem,
            ItemBaseResolutionResult? itemBaseResolution,
            IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions,
            GameDataCatalog? gameDataCatalog)
        {
            CallCount++;
            if (DraftFactory is not null)
            {
                return TradeSearchDraftResult.Success(DraftFactory(parsedItem));
            }

            return TradeSearchDraftResult.Success(new TradeSearchDraft
            {
                ItemClass = parsedItem.ItemClass,
                Rarity = parsedItem.Rarity,
                DisplayName = parsedItem.DisplayName,
                ParsedBaseType = parsedItem.BaseType,
                Base = CreateBaseDraft(itemBaseResolution),
                ItemLevel = parsedItem.ItemLevel,
            });
        }

        private static TradeSearchBaseDraft CreateBaseDraft(
            ItemBaseResolutionResult? itemBaseResolution)
        {
            if (itemBaseResolution is null)
            {
                return new TradeSearchBaseDraft();
            }

            return new TradeSearchBaseDraft
            {
                Status = itemBaseResolution.Status,
                ResolvedBaseId = itemBaseResolution.Status == ItemBaseResolutionStatus.Unknown
                    ? null
                    : itemBaseResolution.ResolvedBaseId,
                ResolvedBaseName = itemBaseResolution.Status == ItemBaseResolutionStatus.Unknown
                    ? null
                    : itemBaseResolution.ResolvedBaseName,
            };
        }
    }

    private sealed class CountingValidator : ITradeSearchDraftValidator
    {
        public int CallCount { get; private set; }

        public TradeSearchValidationResult Validate(TradeSearchDraft draft)
        {
            CallCount++;
            return TradeSearchValidationResult.FromDiagnostics([]);
        }
    }

    private sealed class FakePriceCheckService : IPathOfExileTradePriceCheckService
    {
        public int CallCount { get; private set; }

        public int CategoryLabelLoadCount { get; private set; }

        public int FetchCallCount { get; private set; }

        public Func<TradeSearchDraft, string?>? CategoryDisplayLabelResolver { get; set; }

        public Func<TradeSearchDraft, CancellationToken, Task<string?>>? CategoryLabelLoader { get; set; }

        public Task<PathOfExileTradeFilterCatalogProviderResult> InitializeFilterCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PathOfExileTradeFilterCatalogProviderResult());
        }

        public TradeSearchDraft ResolveEffectiveDraft(TradeSearchDraft draft) => draft;

        public async Task<string?> LoadCategoryDisplayLabelAsync(
            TradeSearchDraft draft,
            CancellationToken cancellationToken = default)
        {
            CategoryLabelLoadCount++;
            var label = CategoryLabelLoader is null
                ? CategoryDisplayLabelResolver?.Invoke(draft)
                : await CategoryLabelLoader(draft, cancellationToken);
            return label;
        }

        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new PathOfExileTradePriceCheckResult
            {
                IsSuccess = true,
                Stage = PathOfExileTradePriceCheckStage.Completed,
                SearchQueryId = "query-1",
                ProviderTotal = 0,
            });
        }

        public Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
            string? searchQueryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default)
        {
            FetchCallCount++;
            throw new InvalidOperationException("Load More is not expected in window lifecycle tests.");
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
                $"poenhance-price-checker-controller-{Guid.NewGuid():N}");
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

internal static class PriceCheckerWindowControllerTestExtensions
{
    public static PriceCheckerWindowUpdateResult ShowOrUpdate(
        this PriceCheckerWindowController controller,
        ParsedItem parsedItem,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions,
        GameDataCatalog? gameDataCatalog = null)
    {
        return controller
            .ShowOrUpdateAsync(parsedItem, itemBaseResolution, modifierResolutions, gameDataCatalog)
            .GetAwaiter()
            .GetResult();
    }
}

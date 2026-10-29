using System.Collections.Immutable;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerMiscellaneousFilterLayoutTests
{
    [Fact]
    public void Defaults_ShowAllApplicableGroupsInRegistryOrder()
    {
        RunOnSta(() =>
        {
            var window = CreateWindow(CreateDraftWithAllRequestedFilters());
            window.UpdateContent(new PriceCheckerWindowState(
                CreateDraftWithAllRequestedFilters(),
                TradeSearchValidationResult.FromDiagnostics([])));
            window.Show();
            window.UpdateLayout();

            var panel = Assert.IsType<WrapPanel>(window.FindName("HeaderRequestedFiltersPanel"));
            Assert.Equal(
                [
                    window.FindName("FracturedStateButton"),
                    window.FindName("ItemLevelFilterBorder"),
                    window.FindName("QualityFilterBorder"),
                    window.FindName("SocketFilterBorder"),
                    window.FindName("LinksFilterBorder"),
                    window.FindName("BasePercentileFilterBorder"),
                ],
                panel.Children.Cast<UIElement>());
            Assert.All(panel.Children.Cast<UIElement>(), child =>
                Assert.Equal(Visibility.Visible, child.Visibility));
            window.Close();
        });
    }

    [Fact]
    public void SavedOrderAndOptionalVisibility_DrivePanel2WithoutRemovingSettingsRows()
    {
        RunOnSta(() =>
        {
            var draft = CreateDraftWithAllRequestedFilters() with
            {
                ItemStateCriteria = new TradeItemStateCriteria
                {
                    Fractured = TradeTriState.Yes,
                },
            };
            var preferences = new[]
            {
                new PriceCheckerMiscellaneousFilterPreference(
                    PriceCheckerMiscellaneousFilterRegistry.Links, true, 0),
                new PriceCheckerMiscellaneousFilterPreference(
                    PriceCheckerMiscellaneousFilterRegistry.Quality, true, 1),
                new PriceCheckerMiscellaneousFilterPreference(
                    PriceCheckerMiscellaneousFilterRegistry.FracturedItem, false, 2),
                new PriceCheckerMiscellaneousFilterPreference(
                    PriceCheckerMiscellaneousFilterRegistry.ItemLevel, true, 3),
                new PriceCheckerMiscellaneousFilterPreference(
                    PriceCheckerMiscellaneousFilterRegistry.Sockets, true, 4),
                new PriceCheckerMiscellaneousFilterPreference(
                    PriceCheckerMiscellaneousFilterRegistry.BasePercentile, false, 5),
            };

            Assert.False(PriceCheckerWindow.ShouldShowMiscellaneousFilterGroup(
                preferences[2],
                draft));
            Assert.False(PriceCheckerWindow.ShouldShowMiscellaneousFilterGroup(
                preferences[5],
                draft));
            Assert.True(PriceCheckerWindow.ShouldShowMiscellaneousFilterGroup(
                preferences[0],
                draft));

            var window = CreateWindow(draft);
            window.UpdateContent(new PriceCheckerWindowState(draft, TradeSearchValidationResult.FromDiagnostics([]))
            {
                MiscellaneousFilterPreferences = preferences,
            });
            window.Show();
            window.UpdateLayout();

            var panel = Assert.IsType<WrapPanel>(window.FindName("HeaderRequestedFiltersPanel"));
            Assert.Equal(
                [
                    window.FindName("LinksFilterBorder"),
                    window.FindName("QualityFilterBorder"),
                    window.FindName("FracturedStateButton"),
                    window.FindName("ItemLevelFilterBorder"),
                    window.FindName("SocketFilterBorder"),
                    window.FindName("BasePercentileFilterBorder"),
                ],
                panel.Children.Cast<UIElement>());
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(window.FindName("LinksFilterBorder")).Visibility);
            Assert.Equal(Visibility.Collapsed, Assert.IsType<Button>(window.FindName("FracturedStateButton")).Visibility);
            Assert.Equal(
                Visibility.Collapsed,
                Assert.IsType<Border>(window.FindName("BasePercentileFilterBorder")).Visibility);
            Assert.Equal(TradeTriState.Yes, draft.ItemStateCriteria.Fractured);
            window.Close();
        });
    }

    [Fact]
    public void NarrowWidth_WrapsCompleteGroupsOntoAdditionalRows()
    {
        RunOnSta(() =>
        {
            var draft = CreateDraftWithAllRequestedFilters();
            var window = CreateWindow(draft);
            window.Width = PriceCheckerPlacementCalculator.UserPanelMinimumWidth;
            window.UpdateContent(new PriceCheckerWindowState(
                draft,
                TradeSearchValidationResult.FromDiagnostics([])));
            window.Show();
            window.UpdateLayout();

            var panel = Assert.IsType<WrapPanel>(window.FindName("HeaderRequestedFiltersPanel"));
            var tops = panel.Children
                .Cast<UIElement>()
                .Where(child => child.Visibility == Visibility.Visible)
                .Select(child => child.TranslatePoint(new Point(0, 0), panel).Y)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            Assert.True(tops.Length >= 2, "Expected Miscellaneous groups to wrap onto multiple rows.");
            Assert.True(panel.ActualHeight > tops.Max() + 8d);
            window.Close();
        });
    }

    private static PriceCheckerWindow CreateWindow(TradeSearchDraft draft)
    {
        return new PriceCheckerWindow
        {
            Width = Math.Max(PriceCheckerPlacementCalculator.UserPanelMinimumWidth, 980),
            Height = 720,
        };
    }

    private static TradeSearchDraft CreateDraftWithAllRequestedFilters()
    {
        return new TradeSearchDraft
        {
            ItemClass = "Body Armours",
            Rarity = "Rare",
            DisplayName = "Misc Layout",
            ParsedBaseType = "Astral Plate",
            RequestedItemFilters =
            [
                new TradeSearchRequestedItemFilter
                {
                    Kind = TradeSearchRequestedItemFilterKind.ItemLevel,
                    Label = "Item Level",
                    CurrentText = "84",
                    RequestedMinimum = 84,
                    IsActive = true,
                    LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
                },
                new TradeSearchRequestedItemFilter
                {
                    Kind = TradeSearchRequestedItemFilterKind.Quality,
                    Label = "Quality",
                    CurrentText = "20",
                    RequestedMinimum = 20,
                    IsActive = true,
                    LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
                },
                new TradeSearchRequestedItemFilter
                {
                    Kind = TradeSearchRequestedItemFilterKind.Sockets,
                    Label = "Sockets",
                    CurrentText = "6",
                    RequestedMinimum = 6,
                    IsActive = true,
                    LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
                },
                new TradeSearchRequestedItemFilter
                {
                    Kind = TradeSearchRequestedItemFilterKind.Links,
                    Label = "Links",
                    CurrentText = "6",
                    RequestedMinimum = 6,
                    IsActive = true,
                    LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
                },
                new TradeSearchRequestedItemFilter
                {
                    Kind = TradeSearchRequestedItemFilterKind.BasePercentile,
                    Label = "Base Percentile",
                    CurrentText = "80",
                    RequestedMinimum = 80,
                    IsActive = true,
                    LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
                },
            ],
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Fractured = TradeTriState.Any,
            },
        };
    }

    private static void RunOnSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured is not null)
        {
            throw new Xunit.Sdk.XunitException(captured.ToString());
        }
    }
}

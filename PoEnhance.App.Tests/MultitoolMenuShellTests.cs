using System.Threading;
using System.Windows;
using System.Windows.Input;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Shell;

namespace PoEnhance.App.Tests;

public sealed class MultitoolMenuShellTests
{
    [Fact]
    public void BareBackslashAndDeveloperShortcuts_RouteToDistinctShellActions()
    {
        using var multitoolService = GlobalHotkeyService.CreateMultitoolMenuService();
        using var developerService = GlobalHotkeyService.CreateDeveloperWindowService();

        Assert.Equal(ShortcutKey.OemBackslash, ShortcutBinding.MultitoolMenu.PrimaryKey);
        Assert.Equal(ShortcutModifiers.None, ShortcutBinding.MultitoolMenu.Modifiers);
        Assert.Equal("\\", ShortcutBinding.MultitoolMenu.ToString());
        Assert.Equal(ShortcutBinding.MultitoolMenu, multitoolService.SelectedShortcut);
        Assert.True(multitoolService.RequiresPathOfExileForeground);

        Assert.Equal(ShortcutKey.OemBackslash, ShortcutBinding.DeveloperWindow.PrimaryKey);
        Assert.Equal(
            ShortcutModifiers.Control | ShortcutModifiers.Shift,
            ShortcutBinding.DeveloperWindow.Modifiers);
        Assert.Equal(ShortcutBinding.DeveloperWindow, developerService.SelectedShortcut);
        Assert.False(developerService.RequiresPathOfExileForeground);

        var hostCode = ReadRepositoryFile("PoEnhance.App", "PoEnhanceApplicationHost.cs");
        Assert.Contains(
            "OnMultitoolMenuHotkeyTriggered",
            hostCode,
            StringComparison.Ordinal);
        Assert.Contains(
            "multitoolMenuWindowController.Toggle()",
            hostCode,
            StringComparison.Ordinal);
        Assert.Contains(
            "developerWindowController.Toggle()",
            hostCode,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TrayLeftDoubleClick_RequestsMultitoolMenuOnly()
    {
        RunOnSta(() =>
        {
            var drawingIcon = new System.Drawing.Icon(
                RepositoryPath("PoEnhance.App", "Assets", "poenhance.ico"));
            using var icon = new PoEnhanceTrayIcon(drawingIcon);
            var multitoolRequests = 0;
            var developerRequests = 0;
            icon.OpenMultitoolMenuRequested += (_, _) => multitoolRequests++;
            icon.OpenDeveloperWindowRequested += (_, _) => developerRequests++;

            icon.HandleLeftDoubleClick();

            Assert.Equal(1, multitoolRequests);
            Assert.Equal(0, developerRequests);
        });
    }

    [Fact]
    public void RepeatedOpenActions_ReuseSameWindowAndControllerState()
    {
        var window = new FakeMultitoolMenuWindow();
        var controller = CreateController(window);

        controller.ShowAndActivate();
        controller.ShowAndActivate();

        Assert.True(window.IsVisible);
        Assert.Equal(1, window.ShowCount);
        Assert.Equal(2, window.ActivateCount);
        Assert.Equal(2, window.PositionCount);

        controller.Toggle();
        controller.Toggle();

        Assert.True(window.IsVisible);
        Assert.Equal(2, window.ShowCount);
        Assert.Equal(1, window.HideCount);
    }

    [Fact]
    public void CloseButtonAndEscape_HideWithoutClosingWindow()
    {
        RunOnSta(() =>
        {
            var window = new MultitoolMenuWindow(
                global::PoEnhance.App.Infrastructure.Settings.ApplicationLeagueSetting.CreateTransient());
            var closed = false;
            window.Closed += (_, _) => closed = true;

            window.Show();
            window.Close();

            Assert.False(window.IsVisible);
            Assert.False(window.ShowInTaskbar);
            Assert.False(closed);

            window.Show();
            window.HideForEscapeKey();

            Assert.False(window.IsVisible);
            Assert.False(window.ShowInTaskbar);
            Assert.False(closed);

            window.CloseForApplicationExit();
            Assert.True(closed);
        });
    }

    [Fact]
    public void ConfirmedExit_UsesHostSharedShutdownRoute()
    {
        var window = new FakeMultitoolMenuWindow();
        var controller = CreateController(window, confirmExit: () => true);
        var confirmedExitRequests = 0;
        controller.ConfirmedExitRequested += (_, _) => confirmedExitRequests++;

        window.RaiseExitRequested();

        Assert.Equal(1, confirmedExitRequests);

        var hostCode = ReadRepositoryFile("PoEnhance.App", "PoEnhanceApplicationHost.cs");
        var normalizedHostCode = hostCode.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains(
            "OnConfirmedExitRequested(object? sender, EventArgs e)\n    {\n        RequestApplicationShutdown();",
            normalizedHostCode,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(hostCode, "requestApplicationShutdown();"));
    }

    [Fact]
    public void CancelledExit_DoesNotRequestShutdown()
    {
        var window = new FakeMultitoolMenuWindow();
        var controller = CreateController(window, confirmExit: () => false);
        var confirmedExitRequests = 0;
        controller.ConfirmedExitRequested += (_, _) => confirmedExitRequests++;

        window.RaiseExitRequested();

        Assert.Equal(0, confirmedExitRequests);
    }

    [Fact]
    public void Startup_DoesNotShowMultitoolMenu()
    {
        var appCode = ReadRepositoryFile("PoEnhance.App", "App.xaml.cs");
        var hostCode = ReadRepositoryFile("PoEnhance.App", "PoEnhanceApplicationHost.cs");

        Assert.DoesNotContain("MultitoolMenuWindow", appCode, StringComparison.Ordinal);
        Assert.DoesNotContain("multitoolMenuWindow.Show()", hostCode, StringComparison.Ordinal);
        Assert.Contains("trayIcon.Show()", hostCode, StringComparison.Ordinal);
    }

    [Fact]
    public void StartAndSettingsNavigation_SwitchContentAndActiveState()
    {
        RunOnSta(() =>
        {
            var window = new MultitoolMenuWindow(ApplicationLeagueSetting.CreateTransient());

            Assert.True(window.IsStartViewVisible);
            Assert.False(window.IsSettingsViewVisible);
            Assert.True(window.IsMainSidebarVisible);
            Assert.False(window.IsSettingsSidebarVisible);
            Assert.True(window.IsStartNavigationActive);
            Assert.False(window.IsSettingsNavigationActive);

            window.ShowSettingsView();

            Assert.False(window.IsStartViewVisible);
            Assert.True(window.IsSettingsViewVisible);
            Assert.False(window.IsMainSidebarVisible);
            Assert.True(window.IsSettingsSidebarVisible);
            Assert.False(window.IsStartNavigationActive);
            Assert.True(window.IsSettingsNavigationActive);
            Assert.True(window.IsSettingsGeneralContentVisible);
            Assert.False(window.IsSettingsHotkeysContentVisible);
            Assert.False(window.IsSettingsPriceCheckerContentVisible);
            Assert.True(window.IsGeneralSettingsNavigationActive);

            window.ShowStartView();

            Assert.True(window.IsStartViewVisible);
            Assert.False(window.IsSettingsViewVisible);
            Assert.True(window.IsMainSidebarVisible);
            Assert.False(window.IsSettingsSidebarVisible);
            Assert.True(window.IsStartNavigationActive);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void SettingsSubpages_SwitchContentWithoutSavingPendingChanges()
    {
        RunOnSta(() =>
        {
            var setting = ApplicationLeagueSetting.CreateTransient("Standard");
            var window = new MultitoolMenuWindow(setting, LeagueProvider());
            window.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();
            var quickUseChangeCount = 0;
            setting.QuickUseCommandsChanged += (_, _) => quickUseChangeCount++;

            window.ShowSettingsView();
            window.SelectPendingLeague("Current League");
            window.AddPendingQuickUseCommand();
            window.BeginHotkeyCapture(0);
            Assert.True(window.CapturePendingHotkey(Key.F8, ModifierKeys.None));

            window.ShowSettingsHotkeysSubpage();
            Assert.False(window.IsSettingsGeneralContentVisible);
            Assert.True(window.IsSettingsHotkeysContentVisible);
            Assert.True(window.IsHotkeysSettingsNavigationActive);

            window.ShowSettingsPriceCheckerSubpage();
            Assert.True(window.IsSettingsPriceCheckerContentVisible);
            Assert.True(window.IsPriceCheckerSettingsNavigationActive);
            Assert.False(window.IsSettingsHotkeysContentVisible);

            window.ShowSettingsGeneralSubpage();
            Assert.True(window.IsSettingsGeneralContentVisible);
            Assert.Equal("Current League", window.PendingLeagueChoice);
            Assert.Equal("Standard", setting.EffectiveLeague);
            Assert.Equal(7, window.PendingQuickUseCommands.Count);
            Assert.Equal(6, setting.QuickUseCommands.Count);
            Assert.Equal(ShortcutKey.F8, window.PendingQuickUseCommands[0].Hotkey?.PrimaryKey);
            Assert.Equal(ShortcutKey.F5, setting.QuickUseCommands[0].Hotkey?.PrimaryKey);
            Assert.Equal(0, quickUseChangeCount);

            window.LeaveSettingsView();
            Assert.True(window.IsStartViewVisible);
            Assert.True(window.IsMainSidebarVisible);
            Assert.False(window.IsSettingsSidebarVisible);
            Assert.Equal("Standard", setting.EffectiveLeague);
            Assert.Equal(6, setting.QuickUseCommands.Count);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void SettingsBack_RestoresMainSidebarAndPriorStartView()
    {
        RunOnSta(() =>
        {
            var window = new MultitoolMenuWindow(ApplicationLeagueSetting.CreateTransient());
            window.ShowSettingsView();
            window.ShowSettingsHotkeysSubpage();

            window.LeaveSettingsView();

            Assert.True(window.IsStartViewVisible);
            Assert.False(window.IsSettingsViewVisible);
            Assert.True(window.IsMainSidebarVisible);
            Assert.False(window.IsSettingsSidebarVisible);
            Assert.True(window.IsStartNavigationActive);
            Assert.False(window.IsSettingsNavigationActive);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void SettingsNavigationMarkup_UsesSidebarSubmenuAndPriceCheckerPreferenceEditor()
    {
        var menuXaml = ReadRepositoryFile(
            "PoEnhance.App",
            "Shell",
            "MultitoolMenuWindow.xaml");

        Assert.Contains("x:Name=\"MainSidebarPanel\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsSidebarPanel\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsBackButton\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Back\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GeneralSettingsNavigationButton\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HotkeysSettingsNavigationButton\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PriceCheckerSettingsNavigationButton\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsGeneralContent\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsHotkeysContent\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsPriceCheckerContent\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PriceCheckerMiscFiltersItemsControl\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsToggleSwitchStyle\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsReorderArrowButtonStyle\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Move up\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Move down\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"General\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Hotkeys\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains(
            "Choose which Miscellaneous control groups are visible",
            menuXaml,
            StringComparison.Ordinal);
        Assert.Contains("Use the arrows to change their order.", menuXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Drag the handle to reorder", menuXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowDrop=\"True\"", menuXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PriceCheckerMiscFilterDragHandle", menuXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Item Price Checker", menuXaml, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "navigation placeholder and does not change Price Checker filtering yet",
            menuXaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PriceCheckerMiscellaneousPreferences_ToggleAndArrowReorderPersistWithoutTouchingOtherSettings()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "settings.json");
            var setting = new ApplicationLeagueSetting(path);
            Assert.True(setting.TrySaveResolved("provider-current", "Current League"));
            var window = new MultitoolMenuWindow(setting, LeagueProvider());
            window.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();

            Assert.Equal(
                PriceCheckerMiscellaneousFilterRegistry.CreateDefaults()
                    .Select(preference => preference.Key),
                window.PendingPriceCheckerMiscellaneousFilters.Select(preference => preference.Key));
            Assert.All(
                window.PendingPriceCheckerMiscellaneousFilters,
                preference => Assert.True(preference.IsVisible));

            Assert.True(window.SetPriceCheckerMiscellaneousFilterVisibility(
                PriceCheckerMiscellaneousFilterRegistry.ItemLevel,
                isVisible: false));
            Assert.True(window.SetPriceCheckerMiscellaneousFilterVisibility(
                PriceCheckerMiscellaneousFilterRegistry.ItemLevel,
                isVisible: true));
            Assert.True(window.SetPriceCheckerMiscellaneousFilterVisibility(
                PriceCheckerMiscellaneousFilterRegistry.Quality,
                isVisible: false));

            // Default order: Fractured, ItemLevel, Quality, Sockets, Links, BasePercentile
            Assert.True(window.MovePriceCheckerMiscellaneousFilterUp(
                PriceCheckerMiscellaneousFilterRegistry.Sockets));
            Assert.Equal(
                [
                    PriceCheckerMiscellaneousFilterRegistry.FracturedItem,
                    PriceCheckerMiscellaneousFilterRegistry.ItemLevel,
                    PriceCheckerMiscellaneousFilterRegistry.Sockets,
                    PriceCheckerMiscellaneousFilterRegistry.Quality,
                    PriceCheckerMiscellaneousFilterRegistry.Links,
                    PriceCheckerMiscellaneousFilterRegistry.BasePercentile,
                ],
                window.PendingPriceCheckerMiscellaneousFilters.Select(preference => preference.Key));
            Assert.False(
                window.PendingPriceCheckerMiscellaneousFilters
                    .Single(preference =>
                        preference.Key == PriceCheckerMiscellaneousFilterRegistry.Quality)
                    .IsVisible);

            Assert.True(window.MovePriceCheckerMiscellaneousFilterDown(
                PriceCheckerMiscellaneousFilterRegistry.Sockets));
            Assert.Equal(
                PriceCheckerMiscellaneousFilterRegistry.CreateDefaults()
                    .Select(preference => preference.Key),
                window.PendingPriceCheckerMiscellaneousFilters.Select(preference => preference.Key));

            Assert.True(window.MovePriceCheckerMiscellaneousFilterUp(
                PriceCheckerMiscellaneousFilterRegistry.Links));
            Assert.True(window.MovePriceCheckerMiscellaneousFilterUp(
                PriceCheckerMiscellaneousFilterRegistry.Links));
            Assert.True(window.MovePriceCheckerMiscellaneousFilterUp(
                PriceCheckerMiscellaneousFilterRegistry.Links));
            Assert.True(window.MovePriceCheckerMiscellaneousFilterUp(
                PriceCheckerMiscellaneousFilterRegistry.Links));
            // Links is first and BasePercentile remains last — boundary moves are no-ops.
            Assert.False(window.MovePriceCheckerMiscellaneousFilterUp(
                PriceCheckerMiscellaneousFilterRegistry.Links));
            Assert.False(window.CanMovePriceCheckerMiscellaneousFilterUp(
                PriceCheckerMiscellaneousFilterRegistry.Links));
            Assert.False(window.MovePriceCheckerMiscellaneousFilterDown(
                PriceCheckerMiscellaneousFilterRegistry.BasePercentile));
            Assert.False(window.CanMovePriceCheckerMiscellaneousFilterDown(
                PriceCheckerMiscellaneousFilterRegistry.BasePercentile));

            window.ShowSettingsView();
            window.ShowSettingsPriceCheckerSubpage();
            window.ShowSettingsHotkeysSubpage();
            window.ShowSettingsGeneralSubpage();
            window.LeaveSettingsView();

            Assert.Equal("Current League", setting.EffectiveLeague);
            Assert.Equal(6, setting.QuickUseCommands.Count);
            Assert.False(
                setting.PriceCheckerMiscellaneousFilters
                    .Single(preference =>
                        preference.Key == PriceCheckerMiscellaneousFilterRegistry.Quality)
                    .IsVisible);
            Assert.Equal(
                [
                    PriceCheckerMiscellaneousFilterRegistry.Links,
                    PriceCheckerMiscellaneousFilterRegistry.FracturedItem,
                    PriceCheckerMiscellaneousFilterRegistry.ItemLevel,
                    PriceCheckerMiscellaneousFilterRegistry.Quality,
                    PriceCheckerMiscellaneousFilterRegistry.Sockets,
                    PriceCheckerMiscellaneousFilterRegistry.BasePercentile,
                ],
                setting.PriceCheckerMiscellaneousFilters.Select(preference => preference.Key));

            var restoredWindow = new MultitoolMenuWindow(
                new ApplicationLeagueSetting(path),
                LeagueProvider());
            Assert.False(
                restoredWindow.PendingPriceCheckerMiscellaneousFilters
                    .Single(preference =>
                        preference.Key == PriceCheckerMiscellaneousFilterRegistry.Quality)
                    .IsVisible);
            Assert.Equal(
                PriceCheckerMiscellaneousFilterRegistry.Links,
                restoredWindow.PendingPriceCheckerMiscellaneousFilters[0].Key);
            Assert.False(restoredWindow.CanMovePriceCheckerMiscellaneousFilterUp(
                PriceCheckerMiscellaneousFilterRegistry.Links));
            Assert.False(restoredWindow.CanMovePriceCheckerMiscellaneousFilterDown(
                PriceCheckerMiscellaneousFilterRegistry.BasePercentile));
            Assert.True(
                restoredWindow.PendingPriceCheckerMiscellaneousFilters
                    .Single(preference =>
                        preference.Key == PriceCheckerMiscellaneousFilterRegistry.ItemLevel)
                    .IsVisible);

            window.CloseForApplicationExit();
            restoredWindow.CloseForApplicationExit();
        });
    }

    [Fact]
    public void PriceCheckerMiscellaneousArrowButtons_RespectFirstAndLastBoundaries()
    {
        RunOnSta(() =>
        {
            var window = new MultitoolMenuWindow(ApplicationLeagueSetting.CreateTransient());
            var first = PriceCheckerMiscellaneousFilterRegistry.FracturedItem;
            var last = PriceCheckerMiscellaneousFilterRegistry.BasePercentile;
            var middle = PriceCheckerMiscellaneousFilterRegistry.Quality;

            Assert.False(window.CanMovePriceCheckerMiscellaneousFilterUp(first));
            Assert.True(window.CanMovePriceCheckerMiscellaneousFilterDown(first));
            Assert.True(window.CanMovePriceCheckerMiscellaneousFilterUp(last));
            Assert.False(window.CanMovePriceCheckerMiscellaneousFilterDown(last));
            Assert.True(window.CanMovePriceCheckerMiscellaneousFilterUp(middle));
            Assert.True(window.CanMovePriceCheckerMiscellaneousFilterDown(middle));

            Assert.False(window.MovePriceCheckerMiscellaneousFilterUp(first));
            Assert.False(window.MovePriceCheckerMiscellaneousFilterDown(last));
            Assert.Equal(
                PriceCheckerMiscellaneousFilterRegistry.CreateDefaults()
                    .Select(preference => preference.Key),
                window.PendingPriceCheckerMiscellaneousFilters.Select(preference => preference.Key));

            Assert.True(window.SetPriceCheckerMiscellaneousFilterVisibility(middle, isVisible: false));
            Assert.True(window.MovePriceCheckerMiscellaneousFilterUp(middle));
            Assert.Equal(
                PriceCheckerMiscellaneousFilterRegistry.ItemLevel,
                window.PendingPriceCheckerMiscellaneousFilters[2].Key);
            Assert.Equal(
                PriceCheckerMiscellaneousFilterRegistry.Quality,
                window.PendingPriceCheckerMiscellaneousFilters[1].Key);
            Assert.False(window.PendingPriceCheckerMiscellaneousFilters[1].IsVisible);

            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void Ui03Preferences_DoNotWireIntoPriceCheckerPopupOrTradeQueryBuilderYet()
    {
        var priceCheckerWindowCode = ReadRepositoryFile(
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerWindow.xaml.cs");
        var priceCheckerWindowXaml = ReadRepositoryFile(
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerWindow.xaml");
        var searchControllerCode = ReadRepositoryFile(
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerSearchController.cs");
        var tradeQueryBuilderCode = ReadRepositoryFile(
            "PoEnhance.App",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeQueryBuilder.cs");

        Assert.DoesNotContain(
            "PriceCheckerMiscellaneousFilters",
            priceCheckerWindowCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PriceCheckerMiscellaneousFilterRegistry",
            priceCheckerWindowCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PriceCheckerMiscellaneousFilters",
            searchControllerCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PriceCheckerMiscellaneousFilters",
            tradeQueryBuilderCode,
            StringComparison.Ordinal);
        Assert.Contains(
            "x:Name=\"HeaderRequestedFiltersPanel\"",
            priceCheckerWindowXaml,
            StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FracturedStateButton\"", priceCheckerWindowXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ItemLevelFilterBorder\"", priceCheckerWindowXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"QualityFilterBorder\"", priceCheckerWindowXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocketFilterBorder\"", priceCheckerWindowXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LinksFilterBorder\"", priceCheckerWindowXaml, StringComparison.Ordinal);
        Assert.Contains(
            "x:Name=\"BasePercentileFilterBorder\"",
            priceCheckerWindowXaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderLeagueSelections_SaveSeparatedIdentityAndDisableCustomInput()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();
            var setting = new ApplicationLeagueSetting(Path.Combine(directory.Path, "settings.json"));
            var window = new MultitoolMenuWindow(setting, LeagueProvider());
            window.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();

            Assert.Equal(
                ["Current League", "Standard"],
                window.LeagueChoices);

            window.SelectPendingLeague("Current League");
            Assert.False(window.IsCustomLeagueEnabled);
            Assert.True(window.ApplyPendingLeague());
            Assert.Equal("Current League", setting.EffectiveLeague);
            Assert.Equal("provider-current", setting.LeagueSelection?.ProviderId);

            Assert.Equal("League saved successfully.", window.LeagueFeedback);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void NonPcRows_AreNotOffered()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();
            var window = new MultitoolMenuWindow(
                new ApplicationLeagueSetting(Path.Combine(directory.Path, "settings.json")),
                LeagueProvider());
            window.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();

            Assert.DoesNotContain("Console Current League", window.LeagueChoices);
            Assert.False(window.IsCustomLeagueEnabled);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void FailedCatalogLoad_DoesNotInjectFallbackLeague()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "settings.json");
            var setting = new ApplicationLeagueSetting(path);
            var window = new MultitoolMenuWindow(
                setting,
                new global::PoEnhance.App.Tests.TestTradeLeagueCatalogProvider(
                    new PathOfExileTradeLeagueCatalogProviderResult()));
            window.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();

            Assert.Empty(window.LeagueChoices);
            Assert.Contains("could not be loaded", window.LeagueFeedback, StringComparison.Ordinal);
            Assert.Null(setting.EffectiveLeague);
            Assert.False(File.Exists(path));
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void SavedAndLegacyLeague_RestoreOnlyWhenCurrentCatalogMatchIsUnique()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();

            var builtInPath = Path.Combine(directory.Path, "built-in.json");
            var builtInSetting = new ApplicationLeagueSetting(builtInPath);
            Assert.True(builtInSetting.TrySaveResolved("provider-current", "Old label"));
            var builtInWindow = new MultitoolMenuWindow(
                new ApplicationLeagueSetting(builtInPath),
                LeagueProvider());
            builtInWindow.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();
            Assert.Equal("Current League", builtInWindow.PendingLeagueChoice);
            Assert.False(builtInWindow.IsCustomLeagueEnabled);
            builtInWindow.CloseForApplicationExit();

            var customPath = Path.Combine(directory.Path, "custom.json");
            var customSetting = new ApplicationLeagueSetting(customPath);
            Assert.True(customSetting.TrySave("Standard"));
            var customWindow = new MultitoolMenuWindow(
                new ApplicationLeagueSetting(customPath),
                LeagueProvider());
            customWindow.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();
            Assert.Equal("Standard", customWindow.PendingLeagueChoice);
            Assert.False(customWindow.IsCustomLeagueEnabled);
            customWindow.CloseForApplicationExit();

            var emptyWindow = new MultitoolMenuWindow(
                new ApplicationLeagueSetting(Path.Combine(directory.Path, "missing.json")),
                LeagueProvider());
            emptyWindow.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();
            Assert.Equal("Select league", emptyWindow.PendingLeagueChoice);
            Assert.False(emptyWindow.IsCustomLeagueEnabled);
            emptyWindow.CloseForApplicationExit();
        });
    }

    [Fact]
    public void PendingLeagueChanges_DoNotChangeActiveLeagueBeforeApply()
    {
        RunOnSta(() =>
        {
            var setting = ApplicationLeagueSetting.CreateTransient("Standard");
            var window = new MultitoolMenuWindow(setting, LeagueProvider());
            window.RefreshLeagueChoicesAsync().GetAwaiter().GetResult();

            window.SelectPendingLeague("Current League");

            Assert.Equal("Standard", setting.EffectiveLeague);
            window.CloseForApplicationExit();
        });
    }

    private static global::PoEnhance.App.Tests.TestTradeLeagueCatalogProvider LeagueProvider() =>
        new(
            new PathOfExileTradeLeagueEntry("provider-current", "Current League", "pc", 0),
            new PathOfExileTradeLeagueEntry("standard-id", "Standard", "pc", 1),
            new PathOfExileTradeLeagueEntry("console-current", "Console Current League", "xbox", 2));

    [Fact]
    public void WindowLayout_ContainsRequiredEnglishModulesAndExitCopy()
    {
        var menuXaml = ReadRepositoryFile(
            "PoEnhance.App",
            "Shell",
            "MultitoolMenuWindow.xaml");
        var exitXaml = ReadRepositoryFile(
            "PoEnhance.App",
            "Shell",
            "ExitConfirmationDialog.xaml");

        var windowCode = ReadRepositoryFile(
            "PoEnhance.App",
            "Shell",
            "MultitoolMenuWindow.xaml.cs");
        var moduleGridStart = menuXaml.IndexOf("<Grid x:Name=\"ModuleGrid\">", StringComparison.Ordinal);
        var moduleGridEnd = menuXaml.IndexOf("<ScrollViewer x:Name=\"SettingsContent\"", StringComparison.Ordinal);
        var moduleGrid = menuXaml[moduleGridStart..moduleGridEnd];

        Assert.Contains("Width=\"1500\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"820\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"1200\"", menuXaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"680\"", menuXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<WrapPanel", menuXaml, StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(moduleGrid, "<ColumnDefinition Width=\"*\" />"));
        Assert.Contains("Grid.Row=\"0\"\n                            Grid.Column=\"0\"", moduleGrid, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"0\"\n                            Grid.Column=\"4\"", moduleGrid, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"1\"\n                            Grid.Column=\"0\"", moduleGrid, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"1\"\n                            Grid.Column=\"2\"", moduleGrid, StringComparison.Ordinal);
        Assert.Contains("ClampWindowSizeTo(workArea)", windowCode, StringComparison.Ordinal);
        Assert.Contains("MinWidth = Math.Min(PreferredMinimumWidth, workArea.Width)", windowCode, StringComparison.Ordinal);
        Assert.Contains("MinHeight = Math.Min(PreferredMinimumHeight, workArea.Height)", windowCode, StringComparison.Ordinal);
        Assert.Contains("Price Checker", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Trade Search", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Currency Exchange", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Economy", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Stash Value", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Regex Tools", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Game Data Browser", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Crafting Tools", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Use Ctrl + D while hovering an item.", menuXaml, StringComparison.Ordinal);
        Assert.Contains("Title=\"Exit PoEnhance?\"", exitXaml, StringComparison.Ordinal);
        Assert.Contains(
            "Are you sure you want to completely close PoEnhance?",
            exitXaml,
            StringComparison.Ordinal);
        Assert.Contains("Content=\"Cancel\"", exitXaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Exit\"", exitXaml, StringComparison.Ordinal);
    }

    private static MultitoolMenuWindowController CreateController(
        FakeMultitoolMenuWindow window,
        Func<bool>? confirmExit = null)
    {
        return new MultitoolMenuWindowController(
            window,
            new FakeClientBoundsProvider(),
            confirmExit ?? (() => true),
            _ => true);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += search.Length;
        }

        return count;
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(RepositoryPath(pathParts));
    }

    private static string RepositoryPath(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. pathParts]);
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

    private sealed class FakeClientBoundsProvider : IPathOfExileClientBoundsProvider
    {
        public bool TryGetClientBounds(out PathOfExileClientBounds bounds)
        {
            bounds = default!;
            return false;
        }
    }

    private sealed class FakeMultitoolMenuWindow : IMultitoolMenuWindow
    {
        public event EventHandler? ExitRequested;

        public bool IsVisible { get; private set; }

        public bool ShowInTaskbar { get; set; }

        public WindowState WindowState { get; set; }

        public int ActivateCount { get; private set; }

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public int PositionCount { get; private set; }

        public IntPtr EnsureHandle() => new(1234);

        public bool Activate()
        {
            ActivateCount++;
            return true;
        }

        public void Show()
        {
            ShowCount++;
            IsVisible = true;
        }

        public void Hide()
        {
            HideCount++;
            IsVisible = false;
        }

        public void PositionForOpen(PathOfExileClientBounds? pathOfExileBounds)
        {
            PositionCount++;
        }

        public void UpdateRuntimeState(
            bool isPathOfExileRunning,
            ShortcutRegistrationState priceCheckerRegistrationState)
        {
        }

        public void CloseForApplicationExit()
        {
            IsVisible = false;
        }

        public void RaiseExitRequested()
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"PoEnhance.MultitoolMenuShellTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

using System.Threading;
using System.Windows;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;
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
            Assert.True(window.IsStartNavigationActive);
            Assert.False(window.IsSettingsNavigationActive);

            window.ShowSettingsView();

            Assert.False(window.IsStartViewVisible);
            Assert.True(window.IsSettingsViewVisible);
            Assert.False(window.IsStartNavigationActive);
            Assert.True(window.IsSettingsNavigationActive);

            window.ShowStartView();

            Assert.True(window.IsStartViewVisible);
            Assert.False(window.IsSettingsViewVisible);
            Assert.True(window.IsStartNavigationActive);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void BuiltInLeagueSelections_SaveTheirEffectiveValueAndDisableCustomInput()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();
            var setting = new ApplicationLeagueSetting(Path.Combine(directory.Path, "settings.json"));
            var window = new MultitoolMenuWindow(setting);

            Assert.Equal(
                ["Standard", "Hardcore", "Ruthless", "Hardcore Ruthless", "Other"],
                window.LeagueChoices);

            foreach (var league in MultitoolMenuWindow.BuiltInLeagueChoices)
            {
                window.SelectPendingLeague(league);

                Assert.False(window.IsCustomLeagueEnabled);
                Assert.True(window.ApplyPendingLeague());
                Assert.Equal(league, setting.EffectiveLeague);
            }

            Assert.Equal("League saved successfully.", window.LeagueFeedback);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void OtherLeague_EnablesCustomInputPreservesTextAndSavesTrimmedValue()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();
            var setting = new ApplicationLeagueSetting(Path.Combine(directory.Path, "settings.json"));
            var window = new MultitoolMenuWindow(setting);

            window.SelectPendingLeague("Other");
            Assert.True(window.IsCustomLeagueEnabled);
            window.SetPendingCustomLeague("  Keepers of the Flame  ");

            window.SelectPendingLeague("Standard");
            Assert.False(window.IsCustomLeagueEnabled);
            Assert.Equal("  Keepers of the Flame  ", window.PendingCustomLeague);
            Assert.Null(setting.EffectiveLeague);

            window.SelectPendingLeague("Other");
            Assert.True(window.ApplyPendingLeague());

            Assert.Equal("Keepers of the Flame", setting.EffectiveLeague);
            Assert.Equal(
                "Keepers of the Flame",
                new ApplicationLeagueSetting(setting.FilePath!).EffectiveLeague);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void EmptyOtherLeague_IsRejectedWithoutPersistence()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "settings.json");
            var setting = new ApplicationLeagueSetting(path);
            var window = new MultitoolMenuWindow(setting);
            window.SelectPendingLeague("Other");
            window.SetPendingCustomLeague("   ");

            Assert.False(window.ApplyPendingLeague());

            Assert.Equal("Enter a league name before applying.", window.LeagueFeedback);
            Assert.Null(setting.EffectiveLeague);
            Assert.False(File.Exists(path));
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void SavedLeague_RestoresBuiltInCustomAndUnselectedStates()
    {
        RunOnSta(() =>
        {
            using var directory = new TemporaryDirectory();

            var builtInPath = Path.Combine(directory.Path, "built-in.json");
            var builtInSetting = new ApplicationLeagueSetting(builtInPath);
            Assert.True(builtInSetting.TrySave("Hardcore"));
            var builtInWindow = new MultitoolMenuWindow(new ApplicationLeagueSetting(builtInPath));
            Assert.Equal("Hardcore", builtInWindow.PendingLeagueChoice);
            Assert.False(builtInWindow.IsCustomLeagueEnabled);
            builtInWindow.CloseForApplicationExit();

            var customPath = Path.Combine(directory.Path, "custom.json");
            var customSetting = new ApplicationLeagueSetting(customPath);
            Assert.True(customSetting.TrySave("Legacy of Phrecia"));
            var customWindow = new MultitoolMenuWindow(new ApplicationLeagueSetting(customPath));
            Assert.Equal("Other", customWindow.PendingLeagueChoice);
            Assert.True(customWindow.IsCustomLeagueEnabled);
            Assert.Equal("Legacy of Phrecia", customWindow.PendingCustomLeague);
            customWindow.CloseForApplicationExit();

            var emptyWindow = new MultitoolMenuWindow(
                new ApplicationLeagueSetting(Path.Combine(directory.Path, "missing.json")));
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
            var window = new MultitoolMenuWindow(setting);

            window.SelectPendingLeague("Other");
            window.SetPendingCustomLeague("Settlers");

            Assert.Equal("Standard", setting.EffectiveLeague);
            window.CloseForApplicationExit();
        });
    }

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
        var moduleGrid = menuXaml[menuXaml.IndexOf("<Grid x:Name=\"ModuleGrid\">", StringComparison.Ordinal)..];

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

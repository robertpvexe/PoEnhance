using System.Text;
using System.Threading;
using System.Windows;
using PoEnhance.App.Infrastructure.Shortcuts;
using PoEnhance.App.Shell;

namespace PoEnhance.App.Tests;

public sealed class ApplicationShellTests
{
    [Fact]
    public void StartupConfiguration_UsesExplicitShutdownAndDoesNotShowDeveloperWindow()
    {
        var appXaml = ReadRepositoryFile("PoEnhance.App", "App.xaml");
        var appCode = ReadRepositoryFile("PoEnhance.App", "App.xaml.cs");
        var hostCode = ReadRepositoryFile("PoEnhance.App", "PoEnhanceApplicationHost.cs");

        Assert.Contains("ShutdownMode=\"OnExplicitShutdown\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("ShutdownMode = ShutdownMode.OnExplicitShutdown", appCode, StringComparison.Ordinal);
        Assert.DoesNotContain(".Show();", appCode, StringComparison.Ordinal);
        Assert.Contains("developerWindow.ShowInTaskbar = false", hostCode, StringComparison.Ordinal);
        Assert.Contains("developerWindow.LoadGameDataAsync", hostCode, StringComparison.Ordinal);
        Assert.Contains("trayIcon.Show()", hostCode, StringComparison.Ordinal);
        Assert.Contains("statusTimer.Start()", hostCode, StringComparison.Ordinal);
        Assert.Contains(
            "PriceCheckerWindowController.UpdateGameOverlayContext",
            hostCode,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StartupConfiguration_AcquiresSingleInstanceBeforeCreatingServices()
    {
        var appCode = ReadRepositoryFile("PoEnhance.App", "App.xaml.cs");
        var guardIndex = appCode.IndexOf("SingleInstanceGuard.TryAcquire", StringComparison.Ordinal);
        var compositionIndex = appCode.IndexOf(
            "PoEnhanceApplicationComposition.CreateDefault",
            StringComparison.Ordinal);

        Assert.True(guardIndex >= 0);
        Assert.True(compositionIndex > guardIndex);
    }

    [Fact]
    public void ExplicitShutdown_DisposesPlatformResourcesAndClosesBothWindowKinds()
    {
        var hostCode = ReadRepositoryFile("PoEnhance.App", "PoEnhanceApplicationHost.cs");

        Assert.Contains("statusTimer.Stop()", hostCode, StringComparison.Ordinal);
        Assert.Contains("priceCheckerHotkeyService.Dispose()", hostCode, StringComparison.Ordinal);
        Assert.Contains("developerWindowHotkeyService.Dispose()", hostCode, StringComparison.Ordinal);
        Assert.Contains("trayIcon.Dispose()", hostCode, StringComparison.Ordinal);
        Assert.Contains("composition.PriceCheckerWindowController.Close()", hostCode, StringComparison.Ordinal);
        Assert.Contains("developerWindowController.CloseForApplicationExit()", hostCode, StringComparison.Ordinal);
        Assert.Contains("singleInstanceGuard.Dispose()", hostCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.Exit", hostCode, StringComparison.Ordinal);
    }

    [Fact]
    public void DeveloperShortcut_IsCtrlShiftBackslashAndIndependentOfPoeForeground()
    {
        using var service = GlobalHotkeyService.CreateDeveloperWindowService();

        Assert.Equal(ShortcutKey.OemBackslash, ShortcutBinding.DeveloperWindow.PrimaryKey);
        Assert.Equal(
            ShortcutModifiers.Control | ShortcutModifiers.Shift,
            ShortcutBinding.DeveloperWindow.Modifiers);
        Assert.Equal("Ctrl + Shift + \\", ShortcutBinding.DeveloperWindow.ToString());
        Assert.Equal(ShortcutBinding.DeveloperWindow, service.SelectedShortcut);
        Assert.False(service.RequiresPathOfExileForeground);
        Assert.True(service.SuppressesKeyRepeat);
    }

    [Fact]
    public void PriceCheckerShortcut_RemainsCtrlDAndForegroundRestricted()
    {
        using var service = new GlobalHotkeyService();

        Assert.Equal(ShortcutKey.D, ShortcutBinding.DefaultPriceChecker.PrimaryKey);
        Assert.Equal(ShortcutModifiers.Control, ShortcutBinding.DefaultPriceChecker.Modifiers);
        Assert.Equal(ShortcutBinding.DefaultPriceChecker, service.SelectedShortcut);
        Assert.True(service.RequiresPathOfExileForeground);
        Assert.True(service.SuppressesKeyRepeat);
    }

    [Fact]
    public void DeveloperWindowController_TogglesAndRestoresOneWindow()
    {
        var window = new FakeDeveloperWindow();
        var foregroundHandles = new List<IntPtr>();
        var controller = new DeveloperWindowController(
            window,
            handle =>
            {
                foregroundHandles.Add(handle);
                return true;
            });

        controller.Toggle();

        Assert.True(window.IsVisible);
        Assert.True(window.ShowInTaskbar);
        Assert.Equal(1, window.ShowCount);
        Assert.Equal(1, window.ActivateCount);
        Assert.Equal([window.Handle], foregroundHandles);

        controller.Toggle();

        Assert.False(window.IsVisible);
        Assert.False(window.ShowInTaskbar);
        Assert.Equal(1, window.HideCount);

        window.WindowState = WindowState.Minimized;
        controller.ShowAndActivate();

        Assert.True(window.IsVisible);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(2, window.ShowCount);
        Assert.Equal(2, window.ActivateCount);
    }

    [Fact]
    public void DeveloperWindow_CloseButtonHidesAndSameWindowCanReopen()
    {
        RunOnSta(() =>
        {
            using var composition = PoEnhanceApplicationComposition.CreateDefault();
            var window = new MainWindow(composition);
            var closed = false;
            window.Closed += (_, _) => closed = true;
            var controller = new DeveloperWindowController(window, _ => true);

            controller.ShowAndActivate();
            Assert.True(window.IsVisible);

            window.Close();

            Assert.False(closed);
            Assert.False(window.IsVisible);
            Assert.False(window.ShowInTaskbar);

            controller.ShowAndActivate();

            Assert.True(window.IsVisible);
            Assert.False(closed);

            window.CloseForApplicationExit();
            Assert.True(closed);
        });
    }

    [Fact]
    public void TrayTextAndMenu_MatchReleaseShellRequirements()
    {
        var running = PoEnhanceTrayIcon.CreateToolTipText(isRunning: true);
        var stopped = PoEnhanceTrayIcon.CreateToolTipText(isRunning: false);

        Assert.Equal("PoEnhance — Path of Exile: Running", running);
        Assert.Equal("PoEnhance — Path of Exile: Not running", stopped);
        Assert.True(running.Length <= 63);
        Assert.True(stopped.Length <= 63);
        Assert.Equal(
            ["Open developer window", "Exit PoEnhance"],
            PoEnhanceTrayIcon.MenuItemTexts);
    }

    [Fact]
    public void SingleInstanceGuard_RejectsSecondOwnerAndReleasesOwnership()
    {
        var mutexName = $@"Local\PoEnhance.Tests.{Guid.NewGuid():N}";

        Assert.True(SingleInstanceGuard.TryAcquire(mutexName, out var first));
        Assert.NotNull(first);
        try
        {
            Assert.False(SingleInstanceGuard.TryAcquire(mutexName, out var second));
            Assert.Null(second);
        }
        finally
        {
            first.Dispose();
        }

        Assert.True(SingleInstanceGuard.TryAcquire(mutexName, out var replacement));
        replacement?.Dispose();
    }

    [Fact]
    public void ApplicationIcon_ContainsEveryRequiredEmbeddedSize()
    {
        var iconPath = RepositoryPath("PoEnhance.App", "Assets", "poenhance.ico");
        using var stream = File.OpenRead(iconPath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        var count = reader.ReadUInt16();
        var sizes = new List<int>(count);
        var entries = new List<(uint Length, uint Offset)>(count);
        for (var index = 0; index < count; index++)
        {
            var width = reader.ReadByte();
            var height = reader.ReadByte();
            _ = reader.ReadByte();
            _ = reader.ReadByte();
            Assert.Equal(1, reader.ReadUInt16());
            Assert.Equal(32, reader.ReadUInt16());
            var length = reader.ReadUInt32();
            var offset = reader.ReadUInt32();
            var resolvedWidth = width == 0 ? 256 : width;
            var resolvedHeight = height == 0 ? 256 : height;
            Assert.Equal(resolvedWidth, resolvedHeight);
            sizes.Add(resolvedWidth);
            entries.Add((length, offset));
        }

        Assert.Equal([16, 20, 24, 32, 48, 64, 128, 256], sizes);
        foreach (var entry in entries)
        {
            stream.Position = entry.Offset;
            Assert.Equal(
                [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
                reader.ReadBytes(8));
            Assert.True(entry.Length > 8);
        }
    }

    [Fact]
    public void ApplicationProject_EmbedsGeneratedIconWithoutRuntimeSourceDependency()
    {
        var project = ReadRepositoryFile("PoEnhance.App", "PoEnhance.App.csproj");
        var window = ReadRepositoryFile("PoEnhance.App", "MainWindow.xaml");

        Assert.Contains("<ApplicationIcon>Assets\\poenhance.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Assets\\poenhance.ico\" />", project, StringComparison.Ordinal);
        Assert.Contains("Icon=\"Assets/poenhance.ico\"", window, StringComparison.Ordinal);
        Assert.Contains(
            "poenhance-gem-source.png\" CopyToOutputDirectory=\"Never\"",
            project,
            StringComparison.Ordinal);
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

    private sealed class FakeDeveloperWindow : IDeveloperWindow
    {
        public IntPtr Handle { get; } = new(1234);

        public bool IsVisible { get; private set; }

        public bool ShowInTaskbar { get; set; }

        public WindowState WindowState { get; set; }

        public int ActivateCount { get; private set; }

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public IntPtr EnsureHandle() => Handle;

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

        public void CloseForApplicationExit()
        {
            IsVisible = false;
        }
    }
}

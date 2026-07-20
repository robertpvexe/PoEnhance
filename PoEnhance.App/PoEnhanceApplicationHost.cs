using System.Windows.Threading;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Features.QuickUse;
using PoEnhance.App.Infrastructure.Input;
using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Shortcuts;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Shell;
using Serilog;

namespace PoEnhance.App;

internal sealed class PoEnhanceApplicationHost : IDisposable
{
    private readonly PoEnhanceApplicationComposition composition;
    private readonly SingleInstanceGuard singleInstanceGuard;
    private readonly MainWindow developerWindow;
    private readonly DeveloperWindowController developerWindowController;
    private readonly MultitoolMenuWindow multitoolMenuWindow;
    private readonly MultitoolMenuWindowController multitoolMenuWindowController;
    private readonly IGlobalHotkeyService priceCheckerHotkeyService;
    private readonly IGlobalHotkeyService developerWindowHotkeyService;
    private readonly IGlobalHotkeyService multitoolMenuHotkeyService;
    private readonly QuickUseHotkeyRuntime quickUseHotkeyRuntime;
    private readonly IPoEnhanceTrayIcon trayIcon;
    private readonly IPathOfExileProcessDetector processDetector;
    private readonly IPathOfExileForegroundWindowDetector foregroundWindowDetector;
    private readonly DispatcherTimer statusTimer;
    private readonly Action requestApplicationShutdown;
    private readonly CancellationTokenSource shutdownCancellation = new();
    private bool? lastPathOfExileForeground;
    private bool? lastPathOfExileRunning;
    private bool isStarted;
    private bool isShuttingDown;
    private bool isDisposed;

    private PoEnhanceApplicationHost(
        PoEnhanceApplicationComposition composition,
        SingleInstanceGuard singleInstanceGuard,
        MainWindow developerWindow,
        DeveloperWindowController developerWindowController,
        MultitoolMenuWindow multitoolMenuWindow,
        MultitoolMenuWindowController multitoolMenuWindowController,
        IGlobalHotkeyService priceCheckerHotkeyService,
        IGlobalHotkeyService developerWindowHotkeyService,
        IGlobalHotkeyService multitoolMenuHotkeyService,
        QuickUseHotkeyRuntime quickUseHotkeyRuntime,
        IPoEnhanceTrayIcon trayIcon,
        IPathOfExileProcessDetector processDetector,
        IPathOfExileForegroundWindowDetector foregroundWindowDetector,
        DispatcherTimer statusTimer,
        Action requestApplicationShutdown)
    {
        this.composition = composition;
        this.singleInstanceGuard = singleInstanceGuard;
        this.developerWindow = developerWindow;
        this.developerWindowController = developerWindowController;
        this.multitoolMenuWindow = multitoolMenuWindow;
        this.multitoolMenuWindowController = multitoolMenuWindowController;
        this.priceCheckerHotkeyService = priceCheckerHotkeyService;
        this.developerWindowHotkeyService = developerWindowHotkeyService;
        this.multitoolMenuHotkeyService = multitoolMenuHotkeyService;
        this.quickUseHotkeyRuntime = quickUseHotkeyRuntime;
        this.trayIcon = trayIcon;
        this.processDetector = processDetector;
        this.foregroundWindowDetector = foregroundWindowDetector;
        this.statusTimer = statusTimer;
        this.requestApplicationShutdown = requestApplicationShutdown;
    }

    public MainWindow DeveloperWindow => developerWindow;

    public static PoEnhanceApplicationHost CreateDefault(
        PoEnhanceApplicationComposition composition,
        SingleInstanceGuard singleInstanceGuard,
        Action requestApplicationShutdown)
    {
        var developerWindow = new MainWindow(composition);
        var multitoolMenuWindow = new MultitoolMenuWindow(composition.LeagueSetting);
        var multitoolMenuWindowController = new MultitoolMenuWindowController(
            multitoolMenuWindow,
            new PathOfExileClientBoundsProvider());
        var foregroundWindowDetector = new PathOfExileForegroundWindowDetector();
        var statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        return new PoEnhanceApplicationHost(
            composition,
            singleInstanceGuard,
            developerWindow,
            new DeveloperWindowController(developerWindow),
            multitoolMenuWindow,
            multitoolMenuWindowController,
            new GlobalHotkeyService(),
            GlobalHotkeyService.CreateDeveloperWindowService(),
            GlobalHotkeyService.CreateMultitoolMenuService(),
            new QuickUseHotkeyRuntime(
                composition.LeagueSetting,
                new KeyboardInputSender(),
                foregroundWindowDetector.IsPathOfExileForegroundWindow,
                () => multitoolMenuWindowController.IsVisible),
            new PoEnhanceTrayIcon(),
            new PathOfExileProcessDetector(),
            foregroundWindowDetector,
            statusTimer,
            requestApplicationShutdown);
    }

    public void Start(IReadOnlyList<string> commandLineArgs)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (isStarted)
        {
            return;
        }

        isStarted = true;
        developerWindow.ShowInTaskbar = false;
        developerWindow.PriceCheckerShortcutChanged += OnPriceCheckerShortcutChanged;
        priceCheckerHotkeyService.Triggered += OnPriceCheckerHotkeyTriggered;
        developerWindowHotkeyService.Triggered += OnDeveloperWindowHotkeyTriggered;
        multitoolMenuHotkeyService.Triggered += OnMultitoolMenuHotkeyTriggered;
        multitoolMenuWindowController.ConfirmedExitRequested += OnConfirmedExitRequested;
        multitoolMenuWindow.HotkeyCaptureStateChanged += OnHotkeyCaptureStateChanged;
        trayIcon.OpenDeveloperWindowRequested += OnOpenDeveloperWindowRequested;
        trayIcon.OpenMultitoolMenuRequested += OnOpenMultitoolMenuRequested;
        trayIcon.ExitRequested += OnExitRequested;
        statusTimer.Tick += OnStatusTimerTick;

        priceCheckerHotkeyService.SetShortcut(developerWindow.SelectedPriceCheckerShortcut);
        priceCheckerHotkeyService.Attach(developerWindow);
        developerWindowHotkeyService.Attach(developerWindow);
        multitoolMenuHotkeyService.Attach(developerWindow);
        quickUseHotkeyRuntime.Attach(developerWindow);

        RefreshPathOfExileState();
        trayIcon.Show();
        statusTimer.Start();

        _ = developerWindow.LoadGameDataAsync(commandLineArgs, shutdownCancellation.Token);
        _ = InitializeBackgroundDiagnosticsAsync();
        _ = PreloadTradeFilterCatalogAsync(
            composition.PriceCheckService,
            shutdownCancellation.Token);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        PrepareForShutdown();
        shutdownCancellation.Dispose();
        isDisposed = true;
    }

    private async Task InitializeBackgroundDiagnosticsAsync()
    {
        try
        {
            await developerWindow.InitializeBackgroundDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Developer diagnostics initialization failed");
        }
    }

    private static async Task PreloadTradeFilterCatalogAsync(
        IPathOfExileTradePriceCheckService priceCheckService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await priceCheckService.InitializeFilterCatalogAsync(cancellationToken);
            if (!result.IsSuccess && !result.IsCancelled)
            {
                var diagnostic = result.Diagnostics.FirstOrDefault();
                Log.Warning(
                    "Trade filter catalog preload failed. {Code}: {Message}",
                    diagnostic?.Code ?? "TRADE_FILTER_CATALOG_UNAVAILABLE",
                    diagnostic?.Message ?? "The Trade filter catalog is unavailable.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Trade filter catalog preload failed");
        }
    }

    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        RefreshPathOfExileState();
    }

    private void RefreshPathOfExileState()
    {
        var isRunning = processDetector.IsPathOfExileRunning();
        var isForeground = foregroundWindowDetector.IsPathOfExileForegroundWindow();

        LogPathOfExileRunningChange(isRunning);
        LogPathOfExileForegroundChange(isForeground);
        priceCheckerHotkeyService.UpdatePathOfExileForegroundState(isForeground);
        multitoolMenuHotkeyService.UpdatePathOfExileForegroundState(
            isForeground || multitoolMenuWindowController.IsVisible);
        quickUseHotkeyRuntime.UpdatePathOfExileForegroundState(isForeground);
        developerWindow.UpdatePathOfExileStatus(
            isRunning,
            isForeground,
            priceCheckerHotkeyService.RegistrationState);
        multitoolMenuWindowController.UpdateRuntimeState(
            isRunning,
            priceCheckerHotkeyService.RegistrationState);
        trayIcon.UpdatePathOfExileState(isRunning);
    }

    private void LogPathOfExileRunningChange(bool isRunning)
    {
        if (lastPathOfExileRunning == isRunning)
        {
            return;
        }

        if (isRunning)
        {
            Log.Information("Path of Exile detected");
        }
        else if (lastPathOfExileRunning is true)
        {
            Log.Information("Path of Exile no longer detected");
        }

        lastPathOfExileRunning = isRunning;
    }

    private void LogPathOfExileForegroundChange(bool isForeground)
    {
        if (lastPathOfExileForeground == isForeground)
        {
            return;
        }

        if (isForeground)
        {
            Log.Information("Path of Exile became the foreground application");
        }
        else if (lastPathOfExileForeground is true)
        {
            Log.Information("Path of Exile is no longer the foreground application");
        }

        lastPathOfExileForeground = isForeground;
    }

    private void OnPriceCheckerShortcutChanged(object? sender, ShortcutBinding shortcut)
    {
        priceCheckerHotkeyService.SetShortcut(shortcut);
        RefreshPathOfExileState();
    }

    private void OnHotkeyCaptureStateChanged(object? sender, bool isCapturing)
    {
        priceCheckerHotkeyService.SetSuspended(isCapturing);
        developerWindowHotkeyService.SetSuspended(isCapturing);
        multitoolMenuHotkeyService.SetSuspended(isCapturing);
        quickUseHotkeyRuntime.SetSuspended(isCapturing);
        if (!isCapturing)
        {
            RefreshPathOfExileState();
        }
    }

    private async void OnPriceCheckerHotkeyTriggered(object? sender, EventArgs e)
    {
        await developerWindow.HandlePriceCheckerShortcutAsync(
            priceCheckerHotkeyService.SelectedShortcut);
    }

    private void OnDeveloperWindowHotkeyTriggered(object? sender, EventArgs e)
    {
        developerWindowController.Toggle();
    }

    private void OnMultitoolMenuHotkeyTriggered(object? sender, EventArgs e)
    {
        var isPathOfExileForeground = foregroundWindowDetector.IsPathOfExileForegroundWindow();
        if (!multitoolMenuWindowController.IsVisible && !isPathOfExileForeground)
        {
            multitoolMenuHotkeyService.UpdatePathOfExileForegroundState(isForeground: false);
            return;
        }

        multitoolMenuWindowController.Toggle();
        multitoolMenuHotkeyService.UpdatePathOfExileForegroundState(
            isPathOfExileForeground || multitoolMenuWindowController.IsVisible);
    }

    private void OnOpenDeveloperWindowRequested(object? sender, EventArgs e)
    {
        developerWindowController.ShowAndActivate();
    }

    private void OnOpenMultitoolMenuRequested(object? sender, EventArgs e)
    {
        multitoolMenuWindowController.ShowAndActivate();
    }

    private void OnConfirmedExitRequested(object? sender, EventArgs e)
    {
        RequestApplicationShutdown();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        RequestApplicationShutdown();
    }

    private void RequestApplicationShutdown()
    {
        PrepareForShutdown();
        requestApplicationShutdown();
    }

    private void PrepareForShutdown()
    {
        if (isShuttingDown)
        {
            return;
        }

        isShuttingDown = true;
        shutdownCancellation.Cancel();
        statusTimer.Stop();
        statusTimer.Tick -= OnStatusTimerTick;
        developerWindow.PriceCheckerShortcutChanged -= OnPriceCheckerShortcutChanged;
        priceCheckerHotkeyService.Triggered -= OnPriceCheckerHotkeyTriggered;
        developerWindowHotkeyService.Triggered -= OnDeveloperWindowHotkeyTriggered;
        multitoolMenuHotkeyService.Triggered -= OnMultitoolMenuHotkeyTriggered;
        multitoolMenuWindowController.ConfirmedExitRequested -= OnConfirmedExitRequested;
        multitoolMenuWindow.HotkeyCaptureStateChanged -= OnHotkeyCaptureStateChanged;
        trayIcon.OpenDeveloperWindowRequested -= OnOpenDeveloperWindowRequested;
        trayIcon.OpenMultitoolMenuRequested -= OnOpenMultitoolMenuRequested;
        trayIcon.ExitRequested -= OnExitRequested;
        priceCheckerHotkeyService.Dispose();
        developerWindowHotkeyService.Dispose();
        multitoolMenuHotkeyService.Dispose();
        quickUseHotkeyRuntime.Dispose();
        trayIcon.Dispose();
        composition.PriceCheckerWindowController.Close();
        multitoolMenuWindowController.CloseForApplicationExit();
        developerWindowController.CloseForApplicationExit();
        composition.Dispose();
        singleInstanceGuard.Dispose();
    }
}

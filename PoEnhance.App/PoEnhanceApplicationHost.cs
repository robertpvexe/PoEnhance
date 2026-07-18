using System.Windows.Threading;
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
    private readonly IGlobalHotkeyService priceCheckerHotkeyService;
    private readonly IGlobalHotkeyService developerWindowHotkeyService;
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
        IGlobalHotkeyService priceCheckerHotkeyService,
        IGlobalHotkeyService developerWindowHotkeyService,
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
        this.priceCheckerHotkeyService = priceCheckerHotkeyService;
        this.developerWindowHotkeyService = developerWindowHotkeyService;
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
        var statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        return new PoEnhanceApplicationHost(
            composition,
            singleInstanceGuard,
            developerWindow,
            new DeveloperWindowController(developerWindow),
            new GlobalHotkeyService(),
            GlobalHotkeyService.CreateDeveloperWindowService(),
            new PoEnhanceTrayIcon(),
            new PathOfExileProcessDetector(),
            new PathOfExileForegroundWindowDetector(),
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
        trayIcon.OpenDeveloperWindowRequested += OnOpenDeveloperWindowRequested;
        trayIcon.ExitRequested += OnExitRequested;
        statusTimer.Tick += OnStatusTimerTick;

        priceCheckerHotkeyService.SetShortcut(developerWindow.SelectedPriceCheckerShortcut);
        priceCheckerHotkeyService.Attach(developerWindow);
        developerWindowHotkeyService.Attach(developerWindow);

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
        developerWindow.UpdatePathOfExileStatus(
            isRunning,
            isForeground,
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

    private async void OnPriceCheckerHotkeyTriggered(object? sender, EventArgs e)
    {
        await developerWindow.HandlePriceCheckerShortcutAsync(
            priceCheckerHotkeyService.SelectedShortcut);
    }

    private void OnDeveloperWindowHotkeyTriggered(object? sender, EventArgs e)
    {
        developerWindowController.Toggle();
    }

    private void OnOpenDeveloperWindowRequested(object? sender, EventArgs e)
    {
        developerWindowController.ShowAndActivate();
    }

    private void OnExitRequested(object? sender, EventArgs e)
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
        trayIcon.OpenDeveloperWindowRequested -= OnOpenDeveloperWindowRequested;
        trayIcon.ExitRequested -= OnExitRequested;
        priceCheckerHotkeyService.Dispose();
        developerWindowHotkeyService.Dispose();
        trayIcon.Dispose();
        composition.PriceCheckerWindowController.Close();
        developerWindowController.CloseForApplicationExit();
        composition.Dispose();
        singleInstanceGuard.Dispose();
    }
}

using System.Windows;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Logging;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core;
using Serilog;

namespace PoEnhance.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly CancellationTokenSource shutdownCancellation = new();
    private PoEnhanceApplicationComposition? applicationComposition;

    protected override void OnStartup(StartupEventArgs e)
    {
        LoggingBootstrap.Configure();

        DispatcherUnhandledException += (_, args) =>
            Log.Error(args.Exception, "Unhandled WPF dispatcher exception");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Log.Fatal(exception, "Unhandled AppDomain exception");
            }
            else
            {
                Log.Fatal("Unhandled AppDomain exception: {ExceptionObject}", args.ExceptionObject);
            }
        };

        Log.Information("{ApplicationName} application starting", ProjectInfo.Name);

        base.OnStartup(e);

        applicationComposition = PoEnhanceApplicationComposition.CreateDefault();
        _ = PreloadTradeFilterCatalogAsync(
            applicationComposition.PriceCheckService,
            shutdownCancellation.Token);
        var mainWindow = new MainWindow(applicationComposition);
        MainWindow = mainWindow;
        mainWindow.Show();

        _ = mainWindow.LoadGameDataAsync(e.Args, shutdownCancellation.Token);
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

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            shutdownCancellation.Cancel();
            applicationComposition?.Dispose();
            Log.Information("PoEnhance application shutting down with exit code {ExitCode}", e.ApplicationExitCode);
        }
        finally
        {
            shutdownCancellation.Dispose();
            LoggingBootstrap.CloseAndFlush();
        }

        base.OnExit(e);
    }
}

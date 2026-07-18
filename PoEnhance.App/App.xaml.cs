using System.Windows;
using PoEnhance.App.Infrastructure.Logging;
using PoEnhance.App.Shell;
using PoEnhance.Core;
using Serilog;

namespace PoEnhance.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private PoEnhanceApplicationHost? applicationHost;

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        if (!SingleInstanceGuard.TryAcquire(out var singleInstanceGuard) ||
            singleInstanceGuard is null)
        {
            Shutdown(0);
            return;
        }

        PoEnhanceApplicationComposition? composition = null;
        try
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

            composition = PoEnhanceApplicationComposition.CreateDefault();
            applicationHost = PoEnhanceApplicationHost.CreateDefault(
                composition,
                singleInstanceGuard,
                () => Shutdown(0));
            MainWindow = applicationHost.DeveloperWindow;
            applicationHost.Start(e.Args);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "PoEnhance application startup failed");
            applicationHost?.Dispose();
            if (applicationHost is null)
            {
                composition?.Dispose();
                singleInstanceGuard.Dispose();
            }

            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            applicationHost?.Dispose();
            Log.Information("PoEnhance application shutting down with exit code {ExitCode}", e.ApplicationExitCode);
        }
        finally
        {
            LoggingBootstrap.CloseAndFlush();
        }

        base.OnExit(e);
    }
}

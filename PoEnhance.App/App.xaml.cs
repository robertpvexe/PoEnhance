using System.Windows;
using PoEnhance.App.Infrastructure.Logging;
using PoEnhance.Core;
using Serilog;

namespace PoEnhance.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("PoEnhance application shutting down with exit code {ExitCode}", e.ApplicationExitCode);
        }
        finally
        {
            LoggingBootstrap.CloseAndFlush();
        }

        base.OnExit(e);
    }
}

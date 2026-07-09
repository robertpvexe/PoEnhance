using System.IO;
using Serilog;
using Serilog.Events;

namespace PoEnhance.App.Infrastructure.Logging;

internal static class LoggingBootstrap
{
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoEnhance",
        "Logs");

    public static void Configure()
    {
        Directory.CreateDirectory(LogDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(GetMinimumLevel())
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(LogDirectory, "poenhance-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
    }

    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }

    private static LogEventLevel GetMinimumLevel()
    {
#if DEBUG
        return LogEventLevel.Debug;
#else
        return LogEventLevel.Information;
#endif
    }
}

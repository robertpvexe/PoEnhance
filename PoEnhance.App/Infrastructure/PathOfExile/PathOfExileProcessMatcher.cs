using System.Diagnostics;

namespace PoEnhance.App.Infrastructure.PathOfExile;

internal static class PathOfExileProcessMatcher
{
    private const string ProcessNamePrefix = "PathOfExile";

    public static bool IsPathOfExileGameProcess(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }

            return IsPathOfExileGameProcessName(process.ProcessName);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool IsPathOfExileGameProcessName(string processName)
    {
        return processName.StartsWith(ProcessNamePrefix, StringComparison.OrdinalIgnoreCase)
            && !IsObviousHelperProcess(processName);
    }

    private static bool IsObviousHelperProcess(string processName)
    {
        return processName.Contains("Crash", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("CrashHandler", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("CrashReporter", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("Handler", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("Reporter", StringComparison.OrdinalIgnoreCase);
    }
}

using System.Diagnostics;

namespace PoEnhance.App.Infrastructure.PathOfExile;

internal sealed class PathOfExileProcessDetector
{
    private const string ProcessNamePrefix = "PathOfExile";

    public bool IsPathOfExileRunning()
    {
        Process[] processes = Process.GetProcesses();

        try
        {
            foreach (Process process in processes)
            {
                if (IsPathOfExileGameProcess(process))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool IsPathOfExileGameProcess(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }

            string processName = process.ProcessName;

            return processName.StartsWith(ProcessNamePrefix, StringComparison.OrdinalIgnoreCase)
                && !IsObviousHelperProcess(processName);
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

    private static bool IsObviousHelperProcess(string processName)
    {
        return processName.Contains("Crash", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("CrashHandler", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("CrashReporter", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("Handler", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("Reporter", StringComparison.OrdinalIgnoreCase);
    }
}

using System.Diagnostics;

namespace PoEnhance.App.Infrastructure.PathOfExile;

internal sealed class PathOfExileProcessDetector : IPathOfExileProcessDetector
{
    public bool IsPathOfExileRunning()
    {
        Process[] processes = Process.GetProcesses();

        try
        {
            foreach (Process process in processes)
            {
                if (PathOfExileProcessMatcher.IsPathOfExileGameProcess(process))
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
}

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PoEnhance.App.Infrastructure.PathOfExile;

internal sealed class PathOfExileForegroundWindowDetector : IPathOfExileForegroundWindowDetector
{
    public bool IsPathOfExileForegroundWindow()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(foregroundWindow, out uint processId);
        if (processId == 0 || processId > int.MaxValue)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);

            return PathOfExileProcessMatcher.IsPathOfExileGameProcess(process);
        }
        catch (ArgumentException)
        {
            return false;
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}

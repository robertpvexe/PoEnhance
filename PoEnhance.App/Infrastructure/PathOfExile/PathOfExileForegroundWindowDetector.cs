using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PoEnhance.App.Infrastructure.PathOfExile;

internal sealed class PathOfExileForegroundWindowDetector : IPathOfExileForegroundWindowDetector
{
    private readonly PathOfExileOverlayWindowRegistry overlayWindowRegistry;
    private readonly Func<IntPtr> getForegroundWindow;
    private readonly Func<IntPtr, bool> isPathOfExileWindow;

    public PathOfExileForegroundWindowDetector()
        : this(
            PathOfExileOverlayWindowRegistry.Shared,
            GetForegroundWindow,
            IsPathOfExileWindow)
    {
    }

    internal PathOfExileForegroundWindowDetector(
        PathOfExileOverlayWindowRegistry overlayWindowRegistry,
        Func<IntPtr> getForegroundWindow,
        Func<IntPtr, bool> isPathOfExileWindow)
    {
        this.overlayWindowRegistry = overlayWindowRegistry ??
            throw new ArgumentNullException(nameof(overlayWindowRegistry));
        this.getForegroundWindow = getForegroundWindow ??
            throw new ArgumentNullException(nameof(getForegroundWindow));
        this.isPathOfExileWindow = isPathOfExileWindow ??
            throw new ArgumentNullException(nameof(isPathOfExileWindow));
    }

    public bool IsPathOfExileForegroundWindow()
    {
        return isPathOfExileWindow(getForegroundWindow());
    }

    public bool IsPathOfExileOverlayContextActive()
    {
        var foregroundWindow = getForegroundWindow();
        return overlayWindowRegistry.Contains(foregroundWindow) ||
            isPathOfExileWindow(foregroundWindow);
    }

    private static bool IsPathOfExileWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(windowHandle, out uint processId);
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

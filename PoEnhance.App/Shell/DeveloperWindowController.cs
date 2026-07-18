using System.Runtime.InteropServices;
using System.Windows;

namespace PoEnhance.App.Shell;

internal sealed class DeveloperWindowController
{
    private readonly IDeveloperWindow window;
    private readonly Func<IntPtr, bool> bringToForeground;

    public DeveloperWindowController(IDeveloperWindow window)
        : this(window, SetForegroundWindow)
    {
    }

    internal DeveloperWindowController(
        IDeveloperWindow window,
        Func<IntPtr, bool> bringToForeground)
    {
        this.window = window;
        this.bringToForeground = bringToForeground;
    }

    public bool IsVisible => window.IsVisible;

    public IntPtr EnsureHandle()
    {
        return window.EnsureHandle();
    }

    public void Toggle()
    {
        if (window.IsVisible)
        {
            Hide();
            return;
        }

        ShowAndActivate();
    }

    public void ShowAndActivate()
    {
        window.ShowInTaskbar = true;
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        _ = window.Activate();
        _ = bringToForeground(window.EnsureHandle());
    }

    public void Hide()
    {
        if (window.IsVisible)
        {
            window.Hide();
        }

        window.ShowInTaskbar = false;
    }

    public void CloseForApplicationExit()
    {
        window.CloseForApplicationExit();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
}

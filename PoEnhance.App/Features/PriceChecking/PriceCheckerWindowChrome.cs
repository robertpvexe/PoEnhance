using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PoEnhance.App.Features.PriceChecking;

internal static class PriceCheckerWindowChrome
{
    public const bool ShowActivated = false;
    public const bool ShowInTaskbar = false;
    public const bool Topmost = true;
    public const ResizeMode ResizeMode = System.Windows.ResizeMode.NoResize;
    public const WindowStyle WindowStyle = System.Windows.WindowStyle.None;
    public const int ExtendedToolWindowStyle = 0x00000080;
    public const int ExtendedAppWindowStyle = 0x00040000;

    private const int ExtendedWindowStyleIndex = -20;

    public static void Apply(Window window)
    {
        window.ResizeMode = ResizeMode;
        window.ShowActivated = ShowActivated;
        window.ShowInTaskbar = ShowInTaskbar;
        window.Topmost = Topmost;
        window.WindowStyle = WindowStyle;
    }

    public static void ApplyToolWindowExtendedStyle(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLongPtr(handle, ExtendedWindowStyleIndex).ToInt64();
        style |= ExtendedToolWindowStyle;
        style &= ~ExtendedAppWindowStyle;
        _ = SetWindowLongPtr(handle, ExtendedWindowStyleIndex, new IntPtr(style));
    }

    private static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : new IntPtr(GetWindowLong32(windowHandle, index));
    }

    private static IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr value)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, value)
            : new IntPtr(SetWindowLong32(windowHandle, index, value.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr windowHandle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr value);
}

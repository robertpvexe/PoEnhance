using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PoEnhance.App.Infrastructure.PathOfExile;
using Serilog;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PathOfExileClientBoundsProvider : IPathOfExileClientBoundsProvider
{
    private const int MonitorDefaultToNearest = 2;
    private const double DefaultDpi = 96d;

    public bool TryGetClientBounds(out PathOfExileClientBounds bounds)
    {
        bounds = default!;

        try
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero || !IsPathOfExileWindow(foregroundWindow))
            {
                return false;
            }

            if (!GetClientRect(foregroundWindow, out RECT clientRect))
            {
                return false;
            }

            var widthPixels = clientRect.Right - clientRect.Left;
            var heightPixels = clientRect.Bottom - clientRect.Top;
            if (widthPixels <= 0 || heightPixels <= 0)
            {
                return false;
            }

            var topLeft = new POINT
            {
                X = clientRect.Left,
                Y = clientRect.Top,
            };
            if (!ClientToScreen(foregroundWindow, ref topLeft))
            {
                return false;
            }

            var dpi = GetWindowDpiOrDefault(foregroundWindow);
            var dpiScale = dpi / DefaultDpi;
            var displayDeviceName = GetMonitorDeviceName(foregroundWindow);

            bounds = new PathOfExileClientBounds(
                topLeft.X / dpiScale,
                topLeft.Y / dpiScale,
                widthPixels / dpiScale,
                heightPixels / dpiScale,
                displayDeviceName,
                dpiScale,
                dpiScale);

            return bounds.IsUsable;
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or ArgumentException)
        {
            Log.Warning(exception, "Path of Exile client bounds were unavailable");
            return false;
        }
    }

    private static bool IsPathOfExileWindow(IntPtr windowHandle)
    {
        _ = GetWindowThreadProcessId(windowHandle, out uint processId);
        if (processId == 0 || processId > int.MaxValue)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return PathOfExileProcessMatcher.IsPathOfExileGameProcess(process);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private static uint GetWindowDpiOrDefault(IntPtr windowHandle)
    {
        try
        {
            var dpi = GetDpiForWindow(windowHandle);
            return dpi == 0 ? (uint)DefaultDpi : dpi;
        }
        catch (EntryPointNotFoundException)
        {
            return (uint)DefaultDpi;
        }
    }

    private static string GetMonitorDeviceName(IntPtr windowHandle)
    {
        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return "unknown-monitor";
        }

        var monitorInfo = new MONITORINFOEX
        {
            Size = Marshal.SizeOf<MONITORINFOEX>(),
        };

        return GetMonitorInfo(monitor, ref monitorInfo)
            ? monitorInfo.DeviceName
            : "unknown-monitor";
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;

        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int Size;

        public RECT Monitor;

        public RECT WorkArea;

        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }
}

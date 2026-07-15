using System.Runtime.InteropServices;

namespace PoEnhance.App.Features.PriceChecking;

internal interface IPriceCheckerDeveloperDiagnosticsPresenter
{
    void ShowOrUpdate(
        PriceCheckerDeveloperDiagnosticsSnapshot snapshot,
        PathOfExileClientBounds clientBounds);

    void Close();
}

internal interface IPriceCheckerDeveloperDiagnosticsWindow
{
    double Width { get; }

    double Height { get; }

    bool IsClosed { get; }

    void UpdateContent(PriceCheckerDeveloperDiagnosticsSnapshot snapshot);

    void ApplyPlacement(PriceCheckerDeveloperDiagnosticsPlacement placement);

    void ShowInactive();

    void Close();
}

internal interface IPriceCheckerDeveloperDiagnosticsWindowFactory
{
    IPriceCheckerDeveloperDiagnosticsWindow CreateWindow();
}

internal interface IPathOfExileMonitorWorkingAreaProvider
{
    bool TryGetWorkingArea(
        PathOfExileClientBounds clientBounds,
        out PriceCheckerMonitorWorkingArea workingArea);
}

internal sealed record PriceCheckerMonitorWorkingArea(
    double Left,
    double Top,
    double Width,
    double Height)
{
    public double Right => Left + Width;
}

internal sealed record PriceCheckerDeveloperDiagnosticsPlacement(double Left, double Top);

internal sealed class PriceCheckerDeveloperDiagnosticsPresenter :
    IPriceCheckerDeveloperDiagnosticsPresenter
{
    internal const double EdgeMargin = 12d;
    private readonly IPathOfExileMonitorWorkingAreaProvider workingAreaProvider;
    private readonly IPriceCheckerDeveloperDiagnosticsWindowFactory windowFactory;
    private IPriceCheckerDeveloperDiagnosticsWindow? window;

    public PriceCheckerDeveloperDiagnosticsPresenter()
        : this(
            new PathOfExileMonitorWorkingAreaProvider(),
            new PriceCheckerDeveloperDiagnosticsWindowFactory())
    {
    }

    internal PriceCheckerDeveloperDiagnosticsPresenter(
        IPathOfExileMonitorWorkingAreaProvider workingAreaProvider,
        IPriceCheckerDeveloperDiagnosticsWindowFactory windowFactory)
    {
        this.workingAreaProvider = workingAreaProvider;
        this.windowFactory = windowFactory;
    }

    public static IPriceCheckerDeveloperDiagnosticsPresenter? CreateForCurrentBuild()
    {
#if DEBUG
        return new PriceCheckerDeveloperDiagnosticsPresenter();
#else
        return null;
#endif
    }

    public void ShowOrUpdate(
        PriceCheckerDeveloperDiagnosticsSnapshot snapshot,
        PathOfExileClientBounds clientBounds)
    {
        if (!workingAreaProvider.TryGetWorkingArea(clientBounds, out var workingArea))
        {
            return;
        }

        if (window is null || window.IsClosed)
        {
            window = windowFactory.CreateWindow();
        }

        window.UpdateContent(snapshot);
        window.ApplyPlacement(CalculatePlacement(
            workingArea,
            window.Width,
            EdgeMargin));
        window.ShowInactive();
    }

    public void Close()
    {
        if (window is { IsClosed: false })
        {
            window.Close();
        }

        window = null;
    }

    internal static PriceCheckerDeveloperDiagnosticsPlacement CalculatePlacement(
        PriceCheckerMonitorWorkingArea workingArea,
        double windowWidth,
        double margin) =>
        new(
            workingArea.Right - windowWidth - margin,
            workingArea.Top + margin);
}

internal sealed class PriceCheckerDeveloperDiagnosticsWindowFactory :
    IPriceCheckerDeveloperDiagnosticsWindowFactory
{
    public IPriceCheckerDeveloperDiagnosticsWindow CreateWindow() =>
        new PriceCheckerDeveloperDiagnosticsWindow();
}

internal sealed class PathOfExileMonitorWorkingAreaProvider :
    IPathOfExileMonitorWorkingAreaProvider
{
    private const uint MonitorDefaultToNearest = 2;

    public bool TryGetWorkingArea(
        PathOfExileClientBounds clientBounds,
        out PriceCheckerMonitorWorkingArea workingArea)
    {
        workingArea = default!;
        if (!clientBounds.IsUsable || clientBounds.DpiScaleX <= 0d || clientBounds.DpiScaleY <= 0d)
        {
            return false;
        }

        var point = new POINT
        {
            X = checked((int)Math.Round((clientBounds.Left + clientBounds.Width / 2d) * clientBounds.DpiScaleX)),
            Y = checked((int)Math.Round((clientBounds.Top + clientBounds.Height / 2d) * clientBounds.DpiScaleY)),
        };
        var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MONITORINFO
        {
            Size = Marshal.SizeOf<MONITORINFO>(),
        };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        workingArea = new PriceCheckerMonitorWorkingArea(
            monitorInfo.WorkArea.Left / clientBounds.DpiScaleX,
            monitorInfo.WorkArea.Top / clientBounds.DpiScaleY,
            (monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left) / clientBounds.DpiScaleX,
            (monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top) / clientBounds.DpiScaleY);
        return true;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int Size;
        public RECT Monitor;
        public RECT WorkArea;
        public uint Flags;
    }
}

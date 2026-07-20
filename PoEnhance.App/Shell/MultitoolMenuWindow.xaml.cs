using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Shortcuts;
using DrawingPoint = System.Drawing.Point;
using FormsScreen = System.Windows.Forms.Screen;

namespace PoEnhance.App.Shell;

internal partial class MultitoolMenuWindow : Window, IMultitoolMenuWindow
{
    private const double DefaultDpi = 96d;
    private const uint MonitorDefaultToNearest = 2;
    private const double PreferredMinimumWidth = 1200d;
    private const double PreferredMinimumHeight = 680d;
    private bool allowApplicationClose;

    public MultitoolMenuWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {GetApplicationVersion()}";
    }

    public event EventHandler? ExitRequested;

    IntPtr IMultitoolMenuWindow.EnsureHandle()
    {
        return new WindowInteropHelper(this).EnsureHandle();
    }

    void IMultitoolMenuWindow.CloseForApplicationExit()
    {
        CloseForApplicationExit();
    }

    public void PositionForOpen(PathOfExileClientBounds? pathOfExileBounds)
    {
        var workArea = pathOfExileBounds?.IsUsable == true
            ? GetWorkAreaForPathOfExile(pathOfExileBounds)
            : GetCurrentMonitorWorkArea();
        ClampWindowSizeTo(workArea);

        if (pathOfExileBounds?.IsUsable == true)
        {
            CenterWithin(
                pathOfExileBounds.Left,
                pathOfExileBounds.Top,
                pathOfExileBounds.Width,
                pathOfExileBounds.Height,
                workArea);
            return;
        }

        CenterWithin(
            workArea.Left,
            workArea.Top,
            workArea.Width,
            workArea.Height,
            workArea);
    }

    public void UpdateRuntimeState(
        bool isPathOfExileRunning,
        ShortcutRegistrationState priceCheckerRegistrationState)
    {
        PathOfExileStatusText.Text = isPathOfExileRunning
            ? "Path of Exile: Running"
            : "Path of Exile: Not running";
        PriceCheckerStatusText.Text = priceCheckerRegistrationState == ShortcutRegistrationState.Active
            ? "Active"
            : "Not active";
    }

    internal void HideForEscapeKey()
    {
        HideForReuse();
    }

    internal void CloseForApplicationExit()
    {
        allowApplicationClose = true;
        Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            HideForEscapeKey();
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!allowApplicationClose)
        {
            e.Cancel = true;
            HideForReuse();
            return;
        }

        base.OnClosing(e);
    }

    private static string GetApplicationVersion()
    {
        var assembly = typeof(MultitoolMenuWindow).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "Unknown";
    }

    private void ExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HideForReuse()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private static MonitorWorkArea GetWorkAreaForPathOfExile(PathOfExileClientBounds bounds)
    {
        var screen = FormsScreen.AllScreens.FirstOrDefault(candidate =>
                string.Equals(candidate.DeviceName, bounds.DisplayDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? FormsScreen.PrimaryScreen
            ?? FormsScreen.FromPoint(new DrawingPoint(0, 0));
        var workArea = screen.WorkingArea;
        return new MonitorWorkArea(
            workArea.Left / bounds.DpiScaleX,
            workArea.Top / bounds.DpiScaleY,
            workArea.Width / bounds.DpiScaleX,
            workArea.Height / bounds.DpiScaleY);
    }

    private static MonitorWorkArea GetCurrentMonitorWorkArea()
    {
        var cursorPosition = System.Windows.Forms.Cursor.Position;
        var screen = FormsScreen.FromPoint(new DrawingPoint(cursorPosition.X, cursorPosition.Y));
        var monitor = MonitorFromPoint(
            new POINT(cursorPosition.X, cursorPosition.Y),
            MonitorDefaultToNearest);
        var dpiScale = TryGetMonitorDpiScale(monitor);
        var workArea = screen.WorkingArea;
        return new MonitorWorkArea(
            workArea.Left / dpiScale,
            workArea.Top / dpiScale,
            workArea.Width / dpiScale,
            workArea.Height / dpiScale);
    }

    private void ClampWindowSizeTo(MonitorWorkArea workArea)
    {
        MinWidth = Math.Min(PreferredMinimumWidth, workArea.Width);
        MinHeight = Math.Min(PreferredMinimumHeight, workArea.Height);
        Width = Math.Min(Math.Max(Width, MinWidth), workArea.Width);
        Height = Math.Min(Math.Max(Height, MinHeight), workArea.Height);
    }

    private void CenterWithin(
        double left,
        double top,
        double width,
        double height,
        MonitorWorkArea workArea)
    {
        var windowWidth = double.IsNaN(Width) ? ActualWidth : Width;
        var windowHeight = double.IsNaN(Height) ? ActualHeight : Height;
        Left = Clamp(
            left + ((width - windowWidth) / 2d),
            workArea.Left,
            workArea.Right - windowWidth);
        Top = Clamp(
            top + ((height - windowHeight) / 2d),
            workArea.Top,
            workArea.Bottom - windowHeight);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Min(Math.Max(value, minimum), Math.Max(minimum, maximum));
    }

    private static double TryGetMonitorDpiScale(IntPtr monitor)
    {
        if (monitor == IntPtr.Zero)
        {
            return 1d;
        }

        try
        {
            return GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0
                ? dpiX / DefaultDpi
                : 1d;
        }
        catch (EntryPointNotFoundException)
        {
            return 1d;
        }
        catch (DllNotFoundException)
        {
            return 1d;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct POINT(int X, int Y);

    private readonly record struct MonitorWorkArea(
        double Left,
        double Top,
        double Width,
        double Height)
    {
        public double Right => Left + Width;

        public double Bottom => Top + Height;
    }
}

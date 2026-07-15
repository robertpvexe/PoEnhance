using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PoEnhance.App.Features.PriceChecking;

internal partial class PriceCheckerDeveloperDiagnosticsWindow : Window,
    IPriceCheckerDeveloperDiagnosticsWindow
{
    private const int ExtendedWindowStyleIndex = -20;
    private const long ExtendedNoActivateStyle = 0x08000000L;
    private bool isClosed;

    public PriceCheckerDeveloperDiagnosticsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyNonActivatingToolWindowStyle();
        Closed += (_, _) => isClosed = true;
    }

    public bool IsClosed => isClosed;

    public void UpdateContent(PriceCheckerDeveloperDiagnosticsSnapshot snapshot)
    {
        StateText.Text = $"Price Checker: {snapshot.State}";
        DiagnosticCountText.Text = $"Diagnostics: {snapshot.Diagnostics.Count}";
        var latest = snapshot.LatestDiagnostic;
        LatestDiagnosticText.Text = latest is null
            ? string.Empty
            : $"{latest.Code}: {latest.Message}";
        LatestDiagnosticText.ToolTip = LatestDiagnosticText.Text;
    }

    public void ApplyPlacement(PriceCheckerDeveloperDiagnosticsPlacement placement)
    {
        Left = placement.Left;
        Top = placement.Top;
    }

    public void ShowInactive()
    {
        if (!IsVisible)
        {
            Show();
        }
    }

    private void ApplyNonActivatingToolWindowStyle()
    {
        PriceCheckerWindowChrome.ApplyToolWindowExtendedStyle(this);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLongPtr(handle, ExtendedWindowStyleIndex).ToInt64();
        _ = SetWindowLongPtr(
            handle,
            ExtendedWindowStyleIndex,
            new IntPtr(style | ExtendedNoActivateStyle));
    }

    private static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : new IntPtr(GetWindowLong32(windowHandle, index));

    private static IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, value)
            : new IntPtr(SetWindowLong32(windowHandle, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr windowHandle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr value);
}

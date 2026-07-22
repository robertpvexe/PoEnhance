using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PoEnhance.App.Features.PriceChecking;

internal partial class ItemCardPreviewWindow : Window, IOfferCardPreviewWindow
{
    internal const double DefaultWidth = 460d;
    private const double MinimumPracticalHeight = 180d;
    private const double NonScrollableHeightAllowance = 180d;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private bool isClosed;

    public ItemCardPreviewWindow()
    {
        InitializeComponent();
        PriceCheckerWindowChrome.Apply(this);
        CloseButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        SourceInitialized += (_, _) => PriceCheckerWindowChrome.ApplyToolWindowExtendedStyle(this);
        Closed += (_, _) => isClosed = true;
    }

    public event EventHandler? CloseRequested;

    public bool IsClosed => isClosed;

    public OfferCardSnapshot? CurrentSnapshot { get; private set; }

    public OfferCardPreviewSize UpdateContent(OfferCardSnapshot snapshot, double maximumHeight)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!double.IsFinite(maximumHeight) || maximumHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHeight));
        }

        CurrentSnapshot = snapshot;
        DataContext = OfferCardPreviewPresentation.FromSnapshot(snapshot, DateTimeOffset.UtcNow);
        Width = DefaultWidth;
        MaxHeight = maximumHeight;
        ContentScrollViewer.MaxHeight = Math.Max(
            1d,
            maximumHeight - NonScrollableHeightAllowance);
        PreviewRoot.Measure(new Size(DefaultWidth, double.PositiveInfinity));
        var minimumHeight = Math.Min(MinimumPracticalHeight, maximumHeight);
        var desiredHeight = PreviewRoot.DesiredSize.Height;
        if (!double.IsFinite(desiredHeight) || desiredHeight <= 0d)
        {
            desiredHeight = minimumHeight;
        }

        Height = Math.Clamp(desiredHeight, minimumHeight, maximumHeight);
        return new OfferCardPreviewSize(DefaultWidth, Height);
    }

    public void ApplyPlacement(PriceCheckerPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
    }

    public void ShowInactive()
    {
        if (!IsVisible)
        {
            Show();
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            _ = SetWindowPos(
                handle,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SetWindowPosNoActivate |
                SetWindowPosNoMove |
                SetWindowPosNoSize |
                SetWindowPosShowWindow);
        }
    }

    public void HideAndClear()
    {
        CurrentSnapshot = null;
        DataContext = null;
        Hide();
    }

    public new void Close()
    {
        if (isClosed)
        {
            return;
        }

        base.Close();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}

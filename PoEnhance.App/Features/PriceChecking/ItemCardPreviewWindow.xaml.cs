using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using PoEnhance.App.Infrastructure.PathOfExile;

namespace PoEnhance.App.Features.PriceChecking;

internal partial class ItemCardPreviewWindow :
    Window,
    IOfferCardPreviewWindow,
    IPinnedOfferCardWindow
{
    internal const double DefaultWidth = 460d;
    private const double ContentHorizontalAllowance = 30d;
    private const double ScrollViewerVerticalPadding = 16d;
    private const double VerticalChromeHeight = ScrollViewerVerticalPadding + 2d;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private readonly OfferCardWindowMode mode;
    private readonly OfferCardWindowSizeCalculator sizeCalculator = new();
    private IntPtr registeredOverlayWindowHandle;
    private bool isClosed;

    public ItemCardPreviewWindow(OfferCardWindowMode mode)
    {
        this.mode = mode;
        InitializeComponent();
        PriceCheckerWindowChrome.Apply(this);
        CloseButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        PinButton.Click += (_, _) =>
        {
            if (this.mode == OfferCardWindowMode.Preview)
            {
                PinRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                UnpinRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        HeaderDragThumb.DragDelta += OnHeaderDragDelta;
        ConfigureMode();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    public event EventHandler? CloseRequested;

    public event EventHandler? PinRequested;

    public event EventHandler<OfferCardDragDeltaEventArgs>? DragDelta;

    public event EventHandler? UnpinRequested;

    public bool IsClosed => isClosed;

    public OfferCardSnapshot? CurrentSnapshot { get; private set; }

    public PriceCheckerPlacement? CurrentPlacement { get; private set; }

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
        HeaderBorder.Measure(new Size(DefaultWidth, double.PositiveInfinity));
        ItemContentPanel.Measure(new Size(
            DefaultWidth - ContentHorizontalAllowance,
            double.PositiveInfinity));
        TradeFooterBorder.Measure(new Size(DefaultWidth, double.PositiveInfinity));
        var size = sizeCalculator.Calculate(
            maximumHeight,
            HeaderBorder.DesiredSize.Height,
            ItemContentPanel.DesiredSize.Height,
            TradeFooterBorder.DesiredSize.Height,
            VerticalChromeHeight);
        MaxHeight = size.MaximumHeight;
        ContentScrollViewer.MaxHeight =
            size.ContentViewportHeight + ScrollViewerVerticalPadding;
        Height = size.Height;
        PreviewRoot.Measure(new Size(DefaultWidth, Height));
        return new OfferCardPreviewSize(DefaultWidth, Height);
    }

    public void ApplyPlacement(PriceCheckerPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
        CurrentPlacement = placement;
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
        CurrentPlacement = null;
        DataContext = null;
        SetPinFeedback(null);
        Hide();
    }

    public void SetPinFeedback(string? message)
    {
        var normalized = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        PinFeedbackText.Text = normalized;
        PinFeedbackText.Visibility = normalized is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        PinButton.ToolTip = normalized ?? "Pin offer";
    }

    public new void Close()
    {
        if (isClosed)
        {
            return;
        }

        base.Close();
    }

    private void ConfigureMode()
    {
        var isPinned = mode == OfferCardWindowMode.Pinned;
        HeaderDragThumb.Visibility = isPinned ? Visibility.Visible : Visibility.Collapsed;
        PinButton.IsChecked = isPinned;
        PinButton.ToolTip = isPinned ? "Unpin offer" : "Pin offer";
        CloseButton.ToolTip = "Close";
    }

    private void OnHeaderDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (mode != OfferCardWindowMode.Pinned)
        {
            return;
        }

        DragDelta?.Invoke(
            this,
            new OfferCardDragDeltaEventArgs(e.HorizontalChange, e.VerticalChange));
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        PriceCheckerWindowChrome.ApplyNonActivatingToolWindowExtendedStyle(this);
        registeredOverlayWindowHandle = new WindowInteropHelper(this).Handle;
        PathOfExileOverlayWindowRegistry.Shared.Register(registeredOverlayWindowHandle);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        PathOfExileOverlayWindowRegistry.Shared.Unregister(registeredOverlayWindowHandle);
        registeredOverlayWindowHandle = IntPtr.Zero;
        isClosed = true;
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

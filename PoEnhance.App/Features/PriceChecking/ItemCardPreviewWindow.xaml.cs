using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using PoEnhance.App.Infrastructure.PathOfExile;
using Serilog;

namespace PoEnhance.App.Features.PriceChecking;

internal partial class ItemCardPreviewWindow :
    Window,
    IOfferCardPreviewWindow,
    IPinnedOfferCardWindow
{
    internal const double DefaultWidth = OfferCardWindowSizeCalculator.MinimumUsefulWidth;
    private const double ContentHorizontalAllowance = 40d;
    private const double ScrollViewerVerticalPadding = 12d;
    private const double VerticalChromeHeight = ScrollViewerVerticalPadding + 2d;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private readonly OfferCardWindowMode mode;
    private readonly OfferCardWindowSizeCalculator sizeCalculator = new();
    private IntPtr registeredOverlayWindowHandle;
    private bool hasCompletedFirstLayoutPass;
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
        Loaded += (_, _) => hasCompletedFirstLayoutPass = true;
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

    internal OfferCardWindowLayoutDiagnostic? LastLayoutDiagnostic { get; private set; }

    public OfferCardPreviewSize UpdateContent(
        OfferCardSnapshot snapshot,
        double maximumWidth,
        double maximumHeight)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!double.IsFinite(maximumWidth) || maximumWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        }

        if (!double.IsFinite(maximumHeight) || maximumHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHeight));
        }

        CurrentSnapshot = snapshot;
        ContentScrollViewer.MaxHeight = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
        var presentation = OfferCardPreviewPresentation.FromSnapshot(snapshot, DateTimeOffset.UtcNow);
        DataContext = presentation;
        Log.Debug(
            "Trade offer card modifier pipeline. {@ModifierPipeline}",
            OfferCardModifierPipelineDiagnostic.Create(snapshot, presentation));
        return MeasureAfterDataBinding(() => MeasureAndApplyContent(
            snapshot,
            presentation,
            maximumWidth,
            maximumHeight));
    }

    private OfferCardPreviewSize MeasureAndApplyContent(
        OfferCardSnapshot snapshot,
        OfferCardPreviewPresentation presentation,
        double maximumWidth,
        double maximumHeight)
    {
        var measuredContentWidth = MeasureNaturalContentWidth();
        var cardWidth = sizeCalculator.CalculateWidth(measuredContentWidth, maximumWidth);
        Width = cardWidth;
        HeaderBorder.Measure(new Size(cardWidth, double.PositiveInfinity));
        var bodySize = MeasureBodyAfterContainerGeneration(cardWidth);
        TradeFooterBorder.Measure(new Size(cardWidth, double.PositiveInfinity));
        var size = sizeCalculator.Calculate(
            maximumHeight,
            HeaderBorder.DesiredSize.Height,
            bodySize.Height,
            TradeFooterBorder.DesiredSize.Height,
            VerticalChromeHeight);
        MaxHeight = size.MaximumHeight;
        ContentScrollViewer.MaxHeight =
            size.ContentViewportHeight + ScrollViewerVerticalPadding;
        ContentScrollViewer.VerticalScrollBarVisibility = size.IsContentScrollingRequired
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Hidden;
        Height = size.Height;
        PreviewRoot.Measure(new Size(cardWidth, Height));
        PreviewRoot.Arrange(new Rect(0d, 0d, cardWidth, Height));
        PreviewRoot.UpdateLayout();
        LastLayoutDiagnostic = OfferCardWindowLayoutDiagnostic.Create(
            mode,
            snapshot,
            presentation,
            maximumWidth,
            maximumHeight,
            cardWidth,
            Height,
            PreviewRoot,
            HeaderBorder,
            ItemContentPanel,
            ContentScrollViewer,
            TradeFooterBorder,
            MaxHeight,
            ContentScrollViewer.VerticalScrollBarVisibility == ScrollBarVisibility.Auto,
            hasCompletedFirstLayoutPass);
        Log.Debug("Trade offer card layout. {@OfferCardLayout}", LastLayoutDiagnostic);
        return new OfferCardPreviewSize(cardWidth, Height);
    }

    private T MeasureAfterDataBinding<T>(Func<T> measure)
    {
        T? result = default;
        Exception? failure = null;
        var frame = new DispatcherFrame();
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () =>
            {
                try
                {
                    result = measure();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    frame.Continue = false;
                }
            });
        Dispatcher.PushFrame(frame);
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result ?? throw new InvalidOperationException(
            "Trade offer card layout measurement did not complete.");
    }

    private double MeasureNaturalContentWidth()
    {
        HeaderBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ItemContentPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        TradeFooterBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Max(
            HeaderBorder.DesiredSize.Width,
            Math.Max(
                ItemContentPanel.DesiredSize.Width + ContentHorizontalAllowance,
                TradeFooterBorder.DesiredSize.Width));
    }

    private Size MeasureBodyAfterContainerGeneration(double cardWidth)
    {
        var availableWidth = Math.Max(1d, cardWidth - ContentHorizontalAllowance);
        var constraint = new Size(availableWidth, double.PositiveInfinity);
        ItemContentPanel.ApplyTemplate();
        ItemContentPanel.Measure(constraint);
        ItemContentPanel.Arrange(new Rect(
            0d,
            0d,
            availableWidth,
            ItemContentPanel.DesiredSize.Height));
        ItemContentPanel.UpdateLayout();
        ItemContentPanel.Measure(constraint);
        return ItemContentPanel.DesiredSize;
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal partial class PriceCheckerWindow : Window, IPriceCheckerWindow, IPriceCheckerNativeResizeWindow
{
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const string NotDetectedText = "Not detected";
    private bool isClosed;
    private bool isHorizontalResizeActive;

    public PriceCheckerWindow()
    {
        InitializeComponent();
        PriceCheckerWindowChrome.Apply(this);

        PreviewMouseDown += (_, _) =>
            PanelInteraction?.Invoke(this, EventArgs.Empty);
        Activated += (_, _) =>
            PanelActivated?.Invoke(this, EventArgs.Empty);
        Deactivated += (_, _) =>
            PanelDeactivated?.Invoke(this, EventArgs.Empty);
        HorizontalDragThumb.DragDelta += OnHorizontalDragDelta;
        HorizontalDragThumb.DragCompleted += (_, _) =>
            HorizontalDragCompleted?.Invoke(this, EventArgs.Empty);
        HorizontalResizeThumb.DragStarted += (_, _) =>
        {
            isHorizontalResizeActive = true;
            HorizontalResizeStarted?.Invoke(this, EventArgs.Empty);
        };
        HorizontalResizeThumb.DragDelta += OnHorizontalResizeDelta;
        HorizontalResizeThumb.DragCompleted += (_, _) => CompleteHorizontalResize();
        HorizontalResizeThumb.LostMouseCapture += (_, _) => CompleteHorizontalResize();
        PinToggleButton.Checked += OnPinStateChanged;
        PinToggleButton.Unchecked += OnPinStateChanged;
        LeagueTextBox.TextChanged += OnLeagueTextChanged;
        SearchButton.Click += OnSearchButtonClick;
        ResetPositionButton.Click += OnResetPositionButtonClick;
        CloseButton.Click += (_, _) => Close();
        KeyDown += OnKeyDown;
        SourceInitialized += (_, _) => PriceCheckerWindowChrome.ApplyToolWindowExtendedStyle(this);
        Closed += (_, _) => isClosed = true;
    }

    public event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;

    public event EventHandler? PanelActivated;

    public event EventHandler? PanelDeactivated;

    public event EventHandler? PanelInteraction;

    public event EventHandler? SearchRequested;

    public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;

    public event EventHandler<PriceCheckerLeagueChangedEventArgs>? LeagueChanged;

    public event EventHandler<bool>? PinStateChanged;

    public event EventHandler? HorizontalDragCompleted;

    public event EventHandler? HorizontalResizeStarted;

    public event EventHandler<PriceCheckerHorizontalResizeEventArgs>? HorizontalResizeDelta;

    public event EventHandler? HorizontalResizeCompleted;

    public event EventHandler? ResetPositionRequested;

    public bool IsClosed => isClosed;

    public bool IsPinned => PinToggleButton.IsChecked == true;

    public PriceCheckerWindowState? CurrentState { get; private set; }

    public PriceCheckerPlacement? CurrentPlacement { get; private set; }

    public PriceCheckerSearchViewState? CurrentSearchState { get; private set; }

    public PriceCheckerPlacement? GetDisplayedPlacement()
    {
        var width = SelectRenderedDimension(ActualWidth, Width);
        var height = SelectRenderedDimension(ActualHeight, Height);
        if (!double.IsFinite(Left) ||
            !double.IsFinite(Top) ||
            width is null ||
            height is null)
        {
            return CurrentPlacement;
        }

        return new PriceCheckerPlacement(
            Left,
            Top,
            width.Value,
            height.Value);
    }

    public void UpdateContent(PriceCheckerWindowState state)
    {
        CurrentState = state;

        var draft = state.Draft;
        DisplayNameText.Text = DisplayValue(draft.DisplayName);
        BaseTypeText.Text = DisplayValue(draft.Base.Observed?.ExactBaseName ?? draft.ParsedBaseType);
        RarityText.Text = DisplayValue(draft.Rarity);
        ItemLevelText.Text = draft.ItemLevel?.ToString() ?? NotDetectedText;
        BaseResolutionStatusText.Text = FormatBaseStatus(draft.Base);
        ModifierCountText.Text = FormatModifierCount(
            draft.ModifierFilters.Count(modifier => modifier.IsSelected),
            draft.ModifierFilters.Count);
        ListingModeText.Text = draft.ListingMode.ToString();
        ValidationTextBox.Text = FormatValidation(state.ValidationResult);
    }

    public void UpdateSearch(PriceCheckerSearchViewState state)
    {
        CurrentSearchState = state;

        if (!string.Equals(LeagueTextBox.Text, state.LeagueIdentifier, StringComparison.Ordinal))
        {
            LeagueTextBox.Text = state.LeagueIdentifier;
        }

        LeagueTextBox.IsEnabled = !state.IsLoading;
        SearchButton.IsEnabled = state.CanSearch;
        SearchStatusText.Text = state.Message;
        SearchSummaryText.Text = state.Summary;
        ModifierCountText.Text = FormatModifierCount(
            state.SelectedModifierCount,
            state.ModifierCount);
        ModifierListBox.ItemsSource = state.Modifiers;
        OfferListBox.ItemsSource = state.Offers;
        OfferListBox.Visibility = state.Offers.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public void ApplyPlacement(PriceCheckerPlacement placement)
    {
        CurrentPlacement = placement;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
    }

    public bool TryGetNativeBounds(out PriceCheckerNativeRectangle bounds)
    {
        bounds = default!;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero ||
            !GetWindowRect(handle, out var rect))
        {
            return false;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        bounds = new PriceCheckerNativeRectangle(
            rect.Left,
            rect.Top,
            width,
            height);
        return true;
    }

    public bool TryGetCursorScreenX(out double screenX)
    {
        screenX = 0d;
        if (!GetCursorPos(out var point))
        {
            return false;
        }

        screenX = point.X;
        return true;
    }

    public bool TrySetNativeBounds(PriceCheckerNativeRectangle bounds)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var left = (int)Math.Round(bounds.Left);
        var top = (int)Math.Round(bounds.Top);
        var width = Math.Max(1, (int)Math.Round(bounds.Width));
        var height = Math.Max(1, (int)Math.Round(bounds.Height));
        var applied = SetWindowPos(
            handle,
            IntPtr.Zero,
            left,
            top,
            width,
            height,
            SetWindowPosNoZOrder | SetWindowPosNoActivate);
        if (!applied)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        CurrentPlacement = new PriceCheckerPlacement(
            bounds.Left / dpi.DpiScaleX,
            bounds.Top / dpi.DpiScaleY,
            bounds.Width / dpi.DpiScaleX,
            bounds.Height / dpi.DpiScaleY);
        return true;
    }

    public void ShowInactive()
    {
        ShowActivated = false;

        if (!IsVisible)
        {
            Show();
        }
    }

    private void OnHorizontalDragDelta(object sender, DragDeltaEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        HorizontalDragDelta?.Invoke(
            this,
            new PriceCheckerHorizontalDragEventArgs(e.HorizontalChange));
    }

    private void OnHorizontalResizeDelta(object sender, DragDeltaEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        if (!TryGetCursorScreenX(out var cursorScreenX))
        {
            return;
        }

        HorizontalResizeDelta?.Invoke(
            this,
            new PriceCheckerHorizontalResizeEventArgs(e.HorizontalChange, cursorScreenX));
    }

    private void CompleteHorizontalResize()
    {
        if (!isHorizontalResizeActive)
        {
            return;
        }

        isHorizontalResizeActive = false;
        HorizontalResizeCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void OnPinStateChanged(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        PinStateChanged?.Invoke(this, IsPinned);
    }

    private void OnResetPositionButtonClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ResetPositionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLeagueTextChanged(object sender, TextChangedEventArgs e)
    {
        LeagueChanged?.Invoke(this, new PriceCheckerLeagueChangedEventArgs(LeagueTextBox.Text));
    }

    private void OnSearchButtonClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnModifierSelectionClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        if (sender is not CheckBox { DataContext: PriceCheckerModifierViewModel modifier })
        {
            return;
        }

        ModifierSelectionChanged?.Invoke(
            this,
            new PriceCheckerModifierSelectionChangedEventArgs(
                modifier.SourceIndex,
                ((CheckBox)sender).IsChecked == true));
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close();
    }

    private static string FormatValidation(
        Core.Trade.TradeSearchValidationResult validationResult)
    {
        var lines = new List<string>
        {
            validationResult.IsValid
                ? "State: Locally valid"
                : "State: Has local validation errors",
        };

        if (validationResult.Diagnostics.Count == 0)
        {
            lines.Add("Diagnostics: None");
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add("Diagnostics:");
        lines.AddRange(validationResult.Diagnostics.Select(diagnostic =>
        {
            var target = diagnostic.ModifierFilterIndex.HasValue
                ? $" [modifier {diagnostic.ModifierFilterIndex.Value}]"
                : string.Empty;
            return $"{diagnostic.Severity}: {diagnostic.Code}{target} - {diagnostic.Message}";
        }));
        return string.Join(Environment.NewLine, lines);
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? NotDetectedText : value;
    }

    internal static string FormatBaseStatus(TradeSearchBaseDraft baseDraft)
    {
        var status = baseDraft.Status?.ToString() ?? "Parser only";
        return $"{status}; Search: {FormatActiveCriterion(baseDraft.ActiveCriterion)}";
    }

    internal static string FormatActiveCriterion(BaseSearchCriterion? criterion)
    {
        return criterion?.Mode switch
        {
            BaseSearchMode.Category => $"Category: {DisplayValue(criterion.Category)}",
            BaseSearchMode.ExactBase => $"Exact Base: {DisplayValue(criterion.ExactBaseName)}",
            _ => NotDetectedText,
        };
    }

    private static double? SelectRenderedDimension(double actualValue, double requestedValue)
    {
        if (double.IsFinite(actualValue) && actualValue > 0d)
        {
            return actualValue;
        }

        return double.IsFinite(requestedValue) && requestedValue > 0d
            ? requestedValue
            : null;
    }

    private static string FormatModifierCount(
        int selectedCount,
        int totalCount)
    {
        return $"{selectedCount} selected of {totalCount}";
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

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
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal partial class PriceCheckerWindow : Window, IPriceCheckerWindow, IPriceCheckerNativeResizeWindow
{
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const string NotDetectedText = "Not detected";
    private const string ModifierContributorRowTag = "ModifierContributorRow";
    private bool isClosed;
    private bool isHorizontalResizeActive;
    private bool isOfferCapacityReportScheduled;

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
        BaseCriterionButton.Click += OnBaseCriterionButtonClick;
        SearchButton.Click += OnSearchButtonClick;
        LoadMoreButton.Click += OnLoadMoreButtonClick;
        TradeButton.Click += OnTradeButtonClick;
        ResultsPanel.SizeChanged += (_, _) => ScheduleOfferCapacityReport();
        ResetItemButton.Click += OnResetItemButtonClick;
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

    public event EventHandler? LoadMoreRequested;

    public event EventHandler? TradeRequested;

    public event EventHandler<PriceCheckerOfferCapacityChangedEventArgs>? OfferCapacityChanged;

    public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;

    public event EventHandler<PriceCheckerModifierBoundsChangedEventArgs>? ModifierBoundsChanged;

    public event EventHandler<PriceCheckerModifierFilterVariantChangedEventArgs>? ModifierFilterVariantChanged;

    public event EventHandler<PriceCheckerModifierExpansionChangedEventArgs>? ModifierExpansionChanged;

    public event EventHandler? BaseCriterionToggleRequested;

    public event EventHandler<bool>? PinStateChanged;

    public event EventHandler? HorizontalDragCompleted;

    public event EventHandler? HorizontalResizeStarted;

    public event EventHandler<PriceCheckerHorizontalResizeEventArgs>? HorizontalResizeDelta;

    public event EventHandler? HorizontalResizeCompleted;

    public event EventHandler? ResetItemRequested;

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
        TitleDisplayNameText.Text = FormatTitle(
            draft.DisplayName,
            draft.Base.Observed?.ExactBaseName ?? draft.Base.ResolvedBaseName ?? draft.ParsedBaseType);
        TitleDisplayNameText.Foreground = (Brush)FindResource(
            TitleForegroundResourceKey(draft.Rarity));
        ItemLevelText.Text = $"Item Level: {draft.ItemLevel?.ToString() ?? NotDetectedText}";
        BaseCriterionButton.Content = FormatActiveCriterion(
            draft.Base.ActiveCriterion,
            state.Presentation.CategoryDisplayLabel);
        BaseCriterionButton.Tag = draft.Base.ActiveCriterion?.Mode == BaseSearchMode.ExactBase
            ? "ExactBase"
            : "Category";
        SocketMetadataText.Text = $"Sockets: {state.Presentation.SocketText}";
        SocketMetadataText.Visibility = string.IsNullOrWhiteSpace(state.Presentation.SocketText)
            ? Visibility.Collapsed
            : Visibility.Visible;
        LinkMetadataText.Text = $"Links: {state.Presentation.LinkText}";
        LinkMetadataText.Visibility = string.IsNullOrWhiteSpace(state.Presentation.LinkText)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ModifierCountText.Text = FormatModifierCount(
            draft.ModifierFilters.Count(modifier => modifier.IsSelected),
            draft.ModifierFilters.Count);
        ScheduleOfferCapacityReport();
    }

    public void UpdateSearch(PriceCheckerSearchViewState state)
    {
        CurrentSearchState = state;

        SearchButton.IsEnabled = state.CanSearch;
        LoadMoreButton.IsEnabled = state.CanLoadMore;
        LoadMoreButton.Visibility = state.CanLoadMore
            ? Visibility.Visible
            : Visibility.Hidden;
        TradeButton.IsEnabled = state.CanOpenTrade;
        SearchStatusText.Text = state.Message;
        SearchSummaryText.Text = state.Summary;
        ModifierCountText.Text = FormatModifierCount(
            state.SelectedModifierCount,
            state.ModifierCount);
        if (!ReferenceEquals(ModifierListBox.ItemsSource, state.Modifiers))
        {
            ModifierListBox.ItemsSource = state.Modifiers;
        }
        OfferListBox.ItemsSource = state.Offers;
        OfferColumnHeader.Visibility = Visibility.Visible;
        OfferListBox.Visibility = state.Offers.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        ScheduleOfferCapacityReport();
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

    private void OnResetItemButtonClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ResetItemRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnBaseCriterionButtonClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        BaseCriterionToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchButtonClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ReportOfferCapacity();
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoadMoreButtonClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ReportOfferCapacity();
        LoadMoreRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleOfferCapacityReport()
    {
        if (isOfferCapacityReportScheduled)
        {
            return;
        }

        isOfferCapacityReportScheduled = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                isOfferCapacityReportScheduled = false;
                ReportOfferCapacity();
            }));
    }

    private void ReportOfferCapacity()
    {
        var availableHeight = ResultsPanel.RowDefinitions.Count > 1
            ? ResultsPanel.RowDefinitions[1].ActualHeight
            : 0d;
        OfferCapacityChanged?.Invoke(
            this,
            new PriceCheckerOfferCapacityChangedEventArgs(
                PriceCheckerOfferCapacityCalculator.Calculate(availableHeight)));
    }

    private void OnTradeButtonClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        TradeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnModifierSelectionClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        if (sender is not CheckBox checkBox)
        {
            return;
        }

        switch (checkBox.DataContext)
        {
            case PriceCheckerModifierViewModel modifier:
                ModifierSelectionChanged?.Invoke(
                    this,
                    new PriceCheckerModifierSelectionChangedEventArgs(
                        modifier.SourceIndex,
                        checkBox.IsChecked == true));
                break;
            case PriceCheckerModifierContributorViewModel contributor when contributor.IsInteractionEnabled:
                ModifierSelectionChanged?.Invoke(
                    this,
                    new PriceCheckerModifierSelectionChangedEventArgs(
                        contributor.ParentSourceIndex,
                        checkBox.IsChecked == true,
                        contributor.ContributorIndex));
                break;
        }
    }

    private void OnModifierBoundTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || !textBox.IsKeyboardFocusWithin)
        {
            return;
        }

        PanelInteraction?.Invoke(this, EventArgs.Empty);
        switch (textBox.DataContext)
        {
            case PriceCheckerModifierViewModel modifier:
                ModifierBoundsChanged?.Invoke(
                    this,
                    new PriceCheckerModifierBoundsChangedEventArgs(
                        modifier.SourceIndex,
                        modifier.MinimumText,
                        modifier.MaximumText));
                break;
            case PriceCheckerModifierContributorViewModel contributor:
                ModifierBoundsChanged?.Invoke(
                    this,
                    new PriceCheckerModifierBoundsChangedEventArgs(
                        contributor.ParentSourceIndex,
                        contributor.MinimumText,
                        contributor.MaximumText,
                        contributor.ContributorIndex));
                break;
        }
    }

    private void OnModifierFilterVariantDropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox
            {
                SelectedItem: PriceCheckerModifierFilterVariantViewModel selected,
            } comboBox)
        {
            return;
        }

        switch (comboBox.DataContext)
        {
            case PriceCheckerModifierViewModel modifier when !string.Equals(
                modifier.SelectedFilterVariant?.Identity,
                selected.Identity,
                StringComparison.Ordinal):
                PanelInteraction?.Invoke(this, EventArgs.Empty);
                ModifierFilterVariantChanged?.Invoke(
                    this,
                    new PriceCheckerModifierFilterVariantChangedEventArgs(
                        modifier.SourceIndex,
                        selected.Identity));
                break;
        }
    }

    private void OnModifierExpansionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PriceCheckerModifierViewModel modifier })
        {
            return;
        }

        e.Handled = true;
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ModifierExpansionChanged?.Invoke(
            this,
            new PriceCheckerModifierExpansionChangedEventArgs(
                modifier.SourceIndex,
                !modifier.IsExpanded));
    }

    private void OnModifierContributorRowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShouldToggleModifierContributorRowFrom(e.OriginalSource as DependencyObject) ||
            sender is not FrameworkElement
            {
                DataContext: PriceCheckerModifierContributorViewModel contributor,
            } ||
            !contributor.IsInteractionEnabled)
        {
            return;
        }

        e.Handled = true;
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ModifierSelectionChanged?.Invoke(
            this,
            new PriceCheckerModifierSelectionChangedEventArgs(
                contributor.ParentSourceIndex,
                !contributor.IsSelected,
                contributor.ContributorIndex));
    }

    private void OnModifierRowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShouldToggleModifierRowFrom(e.OriginalSource as DependencyObject) ||
            sender is not ListBoxItem { DataContext: PriceCheckerModifierViewModel modifier })
        {
            return;
        }

        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ModifierSelectionChanged?.Invoke(
            this,
            new PriceCheckerModifierSelectionChangedEventArgs(
                modifier.SourceIndex,
                !modifier.IsSelected));
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

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? NotDetectedText : value;
    }

    internal static bool ShouldToggleModifierRowFrom(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ComboBoxItem)
            {
                return false;
            }

            if (source is FrameworkElement { Tag: ModifierContributorRowTag })
            {
                return false;
            }

            if (source is ListBoxItem)
            {
                return true;
            }

            if (source is ButtonBase or TextBoxBase or Selector)
            {
                return false;
            }

            source = InteractiveParent(source);
        }

        return true;
    }

    internal static bool ShouldToggleModifierContributorRowFrom(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ComboBoxItem || source is ButtonBase or TextBoxBase or Selector)
            {
                return false;
            }

            if (source is FrameworkElement { Tag: ModifierContributorRowTag })
            {
                return true;
            }

            source = InteractiveParent(source);
        }

        return false;
    }

    private static DependencyObject? InteractiveParent(DependencyObject source)
    {
        if (source is Visual or Visual3D)
        {
            return VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        if (source is FrameworkContentElement frameworkContent)
        {
            return frameworkContent.Parent ?? frameworkContent.TemplatedParent;
        }

        if (source is ContentElement content)
        {
            return ContentOperations.GetParent(content);
        }

        return LogicalTreeHelper.GetParent(source);
    }

    internal static string FormatActiveCriterion(
        BaseSearchCriterion? criterion,
        string? providerCategoryDisplayLabel = null)
    {
        return criterion?.Mode switch
        {
            BaseSearchMode.Category => $"Item Category: {(providerCategoryDisplayLabel?.Trim() is { Length: > 0 } label ? label : "—")}",
            BaseSearchMode.ExactBase => $"Exact Base: {DisplayValue(criterion.ExactBaseName)}",
            _ => NotDetectedText,
        };
    }

    internal static string FormatTitle(
        string? displayName,
        string? baseName)
    {
        return string.Join(
            ' ',
            new[] { displayName, baseName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
    }

    internal static string TitleForegroundResourceKey(string? rarity)
    {
        return rarity?.Trim() switch
        {
            var value when string.Equals(value, "Magic", StringComparison.OrdinalIgnoreCase) =>
                "PriceCheckerTitleMagicForegroundBrush",
            var value when string.Equals(value, "Rare", StringComparison.OrdinalIgnoreCase) =>
                "PriceCheckerTitleRareForegroundBrush",
            var value when string.Equals(value, "Unique", StringComparison.OrdinalIgnoreCase) =>
                "PriceCheckerTitleUniqueForegroundBrush",
            _ => "PriceCheckerTitleNormalForegroundBrush",
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

    internal static string FormatModifierCount(
        int selectedCount,
        int totalCount)
    {
        return $"{selectedCount} of {totalCount} stats selected";
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

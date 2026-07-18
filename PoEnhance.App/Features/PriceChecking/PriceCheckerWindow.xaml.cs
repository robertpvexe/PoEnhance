using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Globalization;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal partial class PriceCheckerWindow : Window, IPriceCheckerWindow, IPriceCheckerNativeResizeWindow
{
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const string NotDetectedText = "Not detected";
    private const string ModifierContributorRowTag = "ModifierContributorRow";
    private const string GroupedModifierRowTag = "GroupedModifierRow";
    private const string ModifierNameTextTag = "ModifierNameText";
    private bool isClosed;
    private bool isHorizontalResizeActive;
    private bool isOfferCapacityReportScheduled;
    private bool isUpdatingContent;
    private TextBlock? hoverExpandedModifierNameText;

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

    public event EventHandler<PriceCheckerItemPropertySelectionChangedEventArgs>? ItemPropertySelectionChanged;

    public event EventHandler<PriceCheckerItemPropertyBoundsChangedEventArgs>? ItemPropertyBoundsChanged;

    public event EventHandler<PriceCheckerItemPropertyExpansionChangedEventArgs>? ItemPropertyExpansionChanged;

    public event EventHandler<PriceCheckerRequestedItemFilterActivationChangedEventArgs>?
        RequestedItemFilterActivationChanged;

    public event EventHandler<PriceCheckerRequestedItemFilterValueChangedEventArgs>?
        RequestedItemFilterValueChanged;

    public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;

    public event EventHandler<PriceCheckerModifierBoundsChangedEventArgs>? ModifierBoundsChanged;

    public event EventHandler<PriceCheckerModifierFilterVariantChangedEventArgs>? ModifierFilterVariantChanged;

    public event EventHandler<PriceCheckerModifierExpansionChangedEventArgs>? ModifierExpansionChanged;

    public event EventHandler? BaseCriterionToggleRequested;

    public event EventHandler<PriceCheckerItemStateChangedEventArgs>? ItemStateChanged;

    public event EventHandler<PriceCheckerRarityChangedEventArgs>? RarityChanged;

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
        var socketFilter = draft.RequestedItemFilters.FirstOrDefault(filter =>
            filter.Kind == TradeSearchRequestedItemFilterKind.Sockets);
        SocketFilterBorder.Visibility = socketFilter is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        foreach (var requestedFilter in draft.RequestedItemFilters)
        {
            UpdateRequestedItemFilter(requestedFilter);
        }
        var activeCriterionText = FormatActiveCriterion(
            draft.Base.ActiveCriterion,
            state.Presentation.CategoryDisplayLabel);
        BaseCriterionText.Text = activeCriterionText;
        BaseCriterionButton.ToolTip =
            $"{activeCriterionText}{Environment.NewLine}Click to toggle category or exact base";
        BaseCriterionButton.Tag = draft.Base.ActiveCriterion?.Mode == BaseSearchMode.ExactBase
            ? "ExactBase"
            : "Category";
        UpdateRarityControl(draft.Rarity, state.Presentation.IsRarityEditable);
        UpdateItemStateButton(
            MirroredStateButton,
            TradeItemStateKind.Mirrored,
            draft.ItemStateCriteria.Mirrored);
        UpdateItemStateButton(
            CorruptedStateButton,
            TradeItemStateKind.Corrupted,
            draft.ItemStateCriteria.Corrupted);
        UpdateItemStateButton(
            IdentifiedStateButton,
            TradeItemStateKind.Identified,
            draft.ItemStateCriteria.Identified);
        SocketMetadataText.Text = draft.SocketText is null ? string.Empty : $"· {draft.SocketText}";
        SocketMetadataText.Visibility = draft.SocketText is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        BaseRollMetadataText.Text = draft.BaseRollPercentile.HasValue
            ? $"Base Roll: {decimal.Round(draft.BaseRollPercentile.Value, 0, MidpointRounding.AwayFromZero):0}%"
            : string.Empty;
        BaseRollMetadataText.Visibility = draft.BaseRollPercentile.HasValue
            ? Visibility.Visible
            : Visibility.Collapsed;
        StatsCountText.Text = FormatStatsCount(
            draft.ItemProperties.Count(property => property.IsSelected) +
                draft.ModifierFilters.Count(modifier => modifier.IsSelected),
            draft.ItemProperties.Length + draft.ModifierFilters.Count);
        ScheduleOfferCapacityReport();
    }

    public void UpdateSearch(PriceCheckerSearchViewState state)
    {
        var statsRowsAreUnchanged = CurrentSearchState is not null &&
            ReferenceEquals(CurrentSearchState.ItemProperties, state.ItemProperties) &&
            ReferenceEquals(CurrentSearchState.Modifiers, state.Modifiers);
        CurrentSearchState = state;

        SearchButton.IsEnabled = state.CanSearch;
        LoadMoreButton.IsEnabled = state.CanLoadMore;
        LoadMoreButton.Visibility = state.CanLoadMore
            ? Visibility.Visible
            : Visibility.Hidden;
        TradeButton.IsEnabled = state.CanOpenTrade;
        SearchStatusText.Text = state.Message;
        SearchSummaryText.Text = state.Summary;
        StatsCountText.Text = FormatStatsCount(state.SelectedStatsCount, state.StatsCount);
        if (!statsRowsAreUnchanged || StatsListBox.ItemsSource is null)
        {
            StatsListBox.ItemsSource = state.Stats;
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

    private void OnItemStateButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TradeItemStateKind kind } || CurrentState is null)
        {
            return;
        }

        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ItemStateChanged?.Invoke(
            this,
            new PriceCheckerItemStateChangedEventArgs(
                kind,
                CycleItemState(CurrentState.Draft.ItemStateCriteria.Get(kind))));
    }

    private void OnRaritySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingContent ||
            CurrentState?.Presentation.IsRarityEditable != true ||
            RarityComboBox.SelectedItem is not ComboBoxItem { Content: string rarity } ||
            !PriceCheckerRarity.TryNormalizeEditable(rarity, out var normalizedRarity))
        {
            return;
        }

        PanelInteraction?.Invoke(this, EventArgs.Empty);
        RarityChanged?.Invoke(this, new PriceCheckerRarityChangedEventArgs(normalizedRarity));
    }

    private void UpdateRarityControl(string? rarity, bool isEditable)
    {
        RarityComboBox.Visibility = isEditable ? Visibility.Visible : Visibility.Collapsed;
        RarityStaticBorder.Visibility = isEditable ? Visibility.Collapsed : Visibility.Visible;
        RarityStaticText.Text = PriceCheckerRarity.DisplayValue(rarity);
        if (!isEditable)
        {
            return;
        }

        var displayRarity = PriceCheckerRarity.DisplayValue(rarity);
        isUpdatingContent = true;
        try
        {
            RarityComboBox.SelectedItem = RarityComboBox.Items
                .OfType<ComboBoxItem>()
                .First(item => string.Equals(
                    item.Content?.ToString(),
                    displayRarity,
                    StringComparison.Ordinal));
        }
        finally
        {
            isUpdatingContent = false;
        }
    }

    private static void UpdateItemStateButton(
        Button button,
        TradeItemStateKind kind,
        TradeTriState state)
    {
        button.Content = FormatItemState(kind, state);
        button.Tag = kind;
        button.DataContext = state;
    }

    internal static TradeTriState CycleItemState(TradeTriState state) => state switch
    {
        TradeTriState.No => TradeTriState.Yes,
        TradeTriState.Yes => TradeTriState.Any,
        TradeTriState.Any or TradeTriState.Auto => TradeTriState.No,
        _ => TradeTriState.No,
    };

    internal static string FormatItemState(TradeItemStateKind kind, TradeTriState state) =>
        $"{kind}: {state switch
        {
            TradeTriState.Yes => "Yes",
            TradeTriState.No => "No",
            _ => "Any",
        }}";

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

    private void OnItemPropertySelectionClick(object sender, RoutedEventArgs e)
    {
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        if (sender is CheckBox
            {
                DataContext: PriceCheckerItemPropertyViewModel property,
            } checkBox)
        {
            ItemPropertySelectionChanged?.Invoke(
                this,
                new PriceCheckerItemPropertySelectionChangedEventArgs(
                    property.SourceIndex,
                    checkBox.IsChecked == true));
        }
    }

    private void OnItemPropertyBoundTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox
            {
                IsKeyboardFocusWithin: true,
                DataContext: PriceCheckerItemPropertyViewModel property,
            })
        {
            return;
        }

        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ItemPropertyBoundsChanged?.Invoke(
            this,
            new PriceCheckerItemPropertyBoundsChangedEventArgs(
                property.SourceIndex,
                property.MinimumText,
                property.MaximumText));
    }

    private void OnRequestedItemFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { IsKeyboardFocusWithin: true } textBox ||
            !TryGetRequestedItemFilterKind(textBox, out var kind))
        {
            return;
        }

        PanelInteraction?.Invoke(this, EventArgs.Empty);
        RequestedItemFilterValueChanged?.Invoke(
            this,
            new PriceCheckerRequestedItemFilterValueChangedEventArgs(kind, textBox.Text));
    }

    private void OnRequestedItemFilterBorderPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border border ||
            IsInsideTextBox(border, e.OriginalSource as DependencyObject) ||
            !TryGetRequestedItemFilterKind(border, out var kind))
        {
            return;
        }

        var filter = CurrentState?.Draft.RequestedItemFilters.FirstOrDefault(filter =>
            filter.Kind == kind);
        if (filter is null)
        {
            return;
        }

        e.Handled = true;
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        RequestedItemFilterActivationChanged?.Invoke(
            this,
            new PriceCheckerRequestedItemFilterActivationChangedEventArgs(kind, !filter.IsActive));
    }

    private void UpdateRequestedItemFilter(TradeSearchRequestedItemFilter filter)
    {
        if (!TryGetRequestedItemFilterControls(filter.Kind, out var border, out var textBox))
        {
            return;
        }

        border.Tag = filter.IsActive ? "Active" : null;
        border.ToolTip = filter.DiagnosticReason;
        if (!string.Equals(textBox.Text, filter.CurrentText, StringComparison.Ordinal))
        {
            var caretIndex = textBox.CaretIndex;
            textBox.Text = filter.CurrentText;
            if (textBox.IsKeyboardFocusWithin)
            {
                textBox.CaretIndex = Math.Min(caretIndex, textBox.Text.Length);
            }
        }
    }

    private bool TryGetRequestedItemFilterControls(
        TradeSearchRequestedItemFilterKind kind,
        out Border border,
        out TextBox textBox)
    {
        (border, textBox) = kind switch
        {
            TradeSearchRequestedItemFilterKind.ItemLevel =>
                (ItemLevelFilterBorder, ItemLevelFilterTextBox),
            TradeSearchRequestedItemFilterKind.Quality =>
                (QualityFilterBorder, QualityFilterTextBox),
            TradeSearchRequestedItemFilterKind.Links =>
                (LinksFilterBorder, LinksFilterTextBox),
            TradeSearchRequestedItemFilterKind.Sockets =>
                (SocketFilterBorder, SocketFilterTextBox),
            _ => (null!, null!),
        };
        return border is not null && textBox is not null;
    }

    private bool TryGetRequestedItemFilterKind(
        FrameworkElement element,
        out TradeSearchRequestedItemFilterKind kind)
    {
        if (ReferenceEquals(element, ItemLevelFilterBorder) ||
            ReferenceEquals(element, ItemLevelFilterTextBox))
        {
            kind = TradeSearchRequestedItemFilterKind.ItemLevel;
            return true;
        }

        if (ReferenceEquals(element, QualityFilterBorder) ||
            ReferenceEquals(element, QualityFilterTextBox))
        {
            kind = TradeSearchRequestedItemFilterKind.Quality;
            return true;
        }

        if (ReferenceEquals(element, LinksFilterBorder) ||
            ReferenceEquals(element, LinksFilterTextBox))
        {
            kind = TradeSearchRequestedItemFilterKind.Links;
            return true;
        }

        if (ReferenceEquals(element, SocketFilterBorder) ||
            ReferenceEquals(element, SocketFilterTextBox))
        {
            kind = TradeSearchRequestedItemFilterKind.Sockets;
            return true;
        }

        kind = default;
        return false;
    }

    private static bool IsInsideTextBox(FrameworkElement container, DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TextBoxBase)
            {
                return true;
            }

            if (ReferenceEquals(source, container))
            {
                return false;
            }

            source = InteractiveParent(source);
        }

        return false;
    }

    private void OnItemPropertyExpansionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PriceCheckerItemPropertyViewModel property })
        {
            return;
        }

        e.Handled = true;
        PanelInteraction?.Invoke(this, EventArgs.Empty);
        ItemPropertyExpansionChanged?.Invoke(
            this,
            new PriceCheckerItemPropertyExpansionChangedEventArgs(
                property.SourceIndex,
                !property.IsExpanded));
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

    private void OnModifierNameAreaMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border area ||
            FindModifierNameText(area) is not { } textBlock ||
            !IsModifierNameTruncated(textBlock))
        {
            return;
        }

        CollapseHoverExpandedModifierName();
        SetModifierNameHoverState(textBlock, isExpanded: true);
        hoverExpandedModifierNameText = textBlock;
        area.InvalidateMeasure();
    }

    private void OnModifierNameAreaMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border area &&
            ReferenceEquals(hoverExpandedModifierNameText, FindModifierNameText(area)))
        {
            CollapseHoverExpandedModifierName();
            area.InvalidateMeasure();
        }
    }

    private void CollapseHoverExpandedModifierName()
    {
        if (hoverExpandedModifierNameText is not { } textBlock)
        {
            return;
        }

        SetModifierNameHoverState(textBlock, isExpanded: false);
        hoverExpandedModifierNameText = null;
    }

    internal static void SetModifierNameHoverState(TextBlock textBlock, bool isExpanded)
    {
        ArgumentNullException.ThrowIfNull(textBlock);
        textBlock.TextWrapping = isExpanded ? TextWrapping.Wrap : TextWrapping.NoWrap;
        textBlock.TextTrimming = isExpanded ? TextTrimming.None : TextTrimming.CharacterEllipsis;
    }

    internal static bool IsModifierNameTruncated(TextBlock textBlock)
    {
        ArgumentNullException.ThrowIfNull(textBlock);
        if (textBlock.ActualWidth <= 0 || string.IsNullOrWhiteSpace(textBlock.Text))
        {
            return false;
        }

        var typeface = new Typeface(
            textBlock.FontFamily,
            textBlock.FontStyle,
            textBlock.FontWeight,
            textBlock.FontStretch);
        var text = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentUICulture,
            textBlock.FlowDirection,
            typeface,
            textBlock.FontSize,
            Brushes.Transparent,
            VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);
        return text.WidthIncludingTrailingWhitespace > textBlock.ActualWidth + 0.5d;
    }

    private static TextBlock? FindModifierNameText(DependencyObject root)
    {
        if (root is TextBlock { Tag: ModifierNameTextTag } textBlock)
        {
            return textBlock;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var result = FindModifierNameText(VisualTreeHelper.GetChild(root, index));
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void OnStatsRowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement row ||
            !ShouldToggleStatsRowFrom(row, e.OriginalSource as DependencyObject))
        {
            return;
        }

        switch (row.DataContext)
        {
            case PriceCheckerItemPropertyViewModel property when property.IsAvailable:
                ItemPropertySelectionChanged?.Invoke(
                    this,
                    new PriceCheckerItemPropertySelectionChangedEventArgs(
                        property.SourceIndex,
                        !property.IsSelected));
                break;
            case PriceCheckerModifierViewModel modifier:
                ModifierSelectionChanged?.Invoke(
                    this,
                    new PriceCheckerModifierSelectionChangedEventArgs(
                        modifier.SourceIndex,
                        !modifier.IsSelected));
                break;
            case PriceCheckerModifierContributorViewModel contributor when contributor.IsInteractionEnabled:
                ModifierSelectionChanged?.Invoke(
                    this,
                    new PriceCheckerModifierSelectionChangedEventArgs(
                        contributor.ParentSourceIndex,
                        !contributor.IsSelected,
                        contributor.ContributorIndex));
                break;
            default:
                return;
        }

        e.Handled = true;
        PanelInteraction?.Invoke(this, EventArgs.Empty);
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

    internal static bool ShouldToggleStatsRowFrom(FrameworkElement row, DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ComboBoxItem || source is ButtonBase or TextBoxBase or Selector)
            {
                return false;
            }

            if (ReferenceEquals(source, row))
            {
                return true;
            }

            if (source is FrameworkElement { Tag: ModifierContributorRowTag or GroupedModifierRowTag })
            {
                return false;
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
            BaseSearchMode.Category => $"Category: {(providerCategoryDisplayLabel?.Trim() is { Length: > 0 } label ? label : "—")}",
            BaseSearchMode.ExactBase => $"Base: {DisplayValue(criterion.ExactBaseName)}",
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
            var value when string.Equals(value, "Normal", StringComparison.OrdinalIgnoreCase) =>
                "PriceCheckerTitleNormalForegroundBrush",
            _ => "PriceCheckerTitleAnyForegroundBrush",
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

    internal static string FormatStatsCount(int selectedCount, int totalCount)
    {
        return $"Stats {selectedCount} of {totalCount} selected";
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

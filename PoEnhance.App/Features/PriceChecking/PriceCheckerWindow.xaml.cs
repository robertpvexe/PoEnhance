using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PoEnhance.App.Features.PriceChecking;

internal partial class PriceCheckerWindow : Window, IPriceCheckerWindow
{
    private const string NotDetectedText = "Not detected";
    private bool isClosed;

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
            HorizontalResizeStarted?.Invoke(this, EventArgs.Empty);
        HorizontalResizeThumb.DragDelta += OnHorizontalResizeDelta;
        HorizontalResizeThumb.DragCompleted += (_, _) =>
            HorizontalResizeCompleted?.Invoke(this, EventArgs.Empty);
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
        BaseTypeText.Text = DisplayValue(draft.ParsedBaseType);
        RarityText.Text = DisplayValue(draft.Rarity);
        ItemLevelText.Text = draft.ItemLevel?.ToString() ?? NotDetectedText;
        BaseResolutionStatusText.Text = draft.Base.Status?.ToString() ?? "Parser only";
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
        HorizontalResizeDelta?.Invoke(
            this,
            new PriceCheckerHorizontalResizeEventArgs(e.HorizontalChange));
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
}

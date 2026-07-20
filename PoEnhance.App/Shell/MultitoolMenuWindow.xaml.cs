using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Settings;
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
    private const string SelectLeaguePlaceholder = "Select league";
    private const string OtherLeagueChoice = "Other";
    internal static IReadOnlyList<string> BuiltInLeagueChoices { get; } =
    [
        "Standard",
        "Hardcore",
        "Ruthless",
        "Hardcore Ruthless",
    ];

    private readonly ApplicationLeagueSetting leagueSetting;
    private readonly ObservableCollection<QuickUseCommandRow> quickUseRows = [];
    private bool allowApplicationClose;
    private bool isRestoringLeagueSelection;
    private QuickUseCommandRow? capturingQuickUseRow;
    private ShortcutBinding? hotkeyBeforeCapture;

    public MultitoolMenuWindow(ApplicationLeagueSetting leagueSetting)
    {
        this.leagueSetting = leagueSetting ?? throw new ArgumentNullException(nameof(leagueSetting));
        InitializeComponent();
        QuickUseCommandsItemsControl.ItemsSource = quickUseRows;
        VersionText.Text = $"Version {GetApplicationVersion()}";
        RestoreLeagueSelection();
        RestoreQuickUseCommands();
        ShowStartView();
    }

    public event EventHandler? ExitRequested;

    internal event EventHandler<bool>? HotkeyCaptureStateChanged;

    internal bool IsStartViewVisible => StartContent.Visibility == Visibility.Visible;

    internal bool IsSettingsViewVisible => SettingsContent.Visibility == Visibility.Visible;

    internal string? PendingLeagueChoice => SelectedLeagueChoice();

    internal IReadOnlyList<string> LeagueChoices => LeagueComboBox.Items
        .OfType<ComboBoxItem>()
        .Select(item => item.Content as string)
        .Where(choice => choice is not null && choice != SelectLeaguePlaceholder)
        .Select(choice => choice!)
        .ToArray();

    internal string PendingCustomLeague => CustomLeagueTextBox.Text;

    internal bool IsCustomLeagueEnabled => CustomLeagueTextBox.IsEnabled;

    internal string LeagueFeedback => LeagueFeedbackText.Text;

    internal bool IsStartNavigationActive => Equals(StartNavigationButton.Tag, "Active");

    internal bool IsSettingsNavigationActive => Equals(SettingsNavigationButton.Tag, "Active");

    internal IReadOnlyList<QuickUseCommandSetting> PendingQuickUseCommands =>
        quickUseRows.Select(row => row.ToSetting()).ToArray();

    internal string QuickUseFeedback => QuickUseFeedbackText.Text;

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

    internal void ShowStartView()
    {
        CancelHotkeyCapture();
        StartContent.Visibility = Visibility.Visible;
        SettingsContent.Visibility = Visibility.Collapsed;
        StartNavigationButton.Tag = "Active";
        SettingsNavigationButton.Tag = null;
    }

    internal void ShowSettingsView()
    {
        StartContent.Visibility = Visibility.Collapsed;
        SettingsContent.Visibility = Visibility.Visible;
        StartNavigationButton.Tag = null;
        SettingsNavigationButton.Tag = "Active";
    }

    internal void SelectPendingLeague(string choice)
    {
        var item = LeagueComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(candidate => string.Equals(
                candidate.Content as string,
                choice,
                StringComparison.Ordinal));
        if (item is null)
        {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }

        LeagueComboBox.SelectedItem = item;
    }

    internal void SetPendingCustomLeague(string value)
    {
        CustomLeagueTextBox.Text = value;
    }

    internal bool ApplyPendingLeague()
    {
        var selectedChoice = SelectedLeagueChoice();
        if (selectedChoice is null || selectedChoice == SelectLeaguePlaceholder)
        {
            ShowLeagueError("Select a league before applying.");
            return false;
        }

        var effectiveLeague = selectedChoice == OtherLeagueChoice
            ? CustomLeagueTextBox.Text.Trim()
            : selectedChoice;
        if (string.IsNullOrWhiteSpace(effectiveLeague))
        {
            ShowLeagueError("Enter a league name before applying.");
            return false;
        }

        if (!leagueSetting.TrySave(effectiveLeague))
        {
            ShowLeagueError("League could not be saved. Try again.");
            return false;
        }

        LeagueFeedbackText.Text = "League saved successfully.";
        LeagueFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(99, 212, 113));
        return true;
    }

    internal void AddPendingQuickUseCommand()
    {
        quickUseRows.Add(new QuickUseCommandRow(
            new QuickUseCommandSetting(string.Empty, true, null, true)));
        ClearQuickUseFeedback();
    }

    internal bool RemovePendingQuickUseCommand(int index)
    {
        if (index < 0 || index >= quickUseRows.Count || !quickUseRows[index].IsCustom)
        {
            return false;
        }

        if (ReferenceEquals(capturingQuickUseRow, quickUseRows[index]))
        {
            CancelHotkeyCapture();
        }

        quickUseRows.RemoveAt(index);
        ClearQuickUseFeedback();
        return true;
    }

    internal void BeginHotkeyCapture(int index)
    {
        if (index < 0 || index >= quickUseRows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        CancelHotkeyCapture();
        capturingQuickUseRow = quickUseRows[index];
        hotkeyBeforeCapture = capturingQuickUseRow.Hotkey;
        capturingQuickUseRow.IsCapturing = true;
        HotkeyCaptureStateChanged?.Invoke(this, true);
        ClearQuickUseFeedback();
    }

    internal bool CapturePendingHotkey(Key key, ModifierKeys modifiers)
    {
        if (capturingQuickUseRow is null)
        {
            return false;
        }

        if (key == Key.Escape)
        {
            CancelHotkeyCapture();
            return true;
        }

        if (key == Key.Back)
        {
            capturingQuickUseRow.Hotkey = null;
            FinishHotkeyCapture();
            ClearQuickUseFeedback();
            return true;
        }

        if (IsModifierKey(key))
        {
            ShowQuickUseError("Press a non-modifier key with optional Ctrl, Alt, or Shift.");
            return false;
        }

        if ((modifiers & ModifierKeys.Windows) != 0 ||
            !TryCreateShortcutBinding(key, modifiers, out var binding))
        {
            ShowQuickUseError("That key cannot be used for a Quick Use hotkey.");
            return false;
        }

        var pending = quickUseRows
            .Select(row => ReferenceEquals(row, capturingQuickUseRow)
                ? row.ToSetting() with { Hotkey = binding }
                : row.ToSetting())
            .ToArray();
        if (!QuickUseSettingsValidator.TryValidate(pending, out var validationError))
        {
            ShowQuickUseError(validationError);
            return false;
        }

        capturingQuickUseRow.Hotkey = binding;
        FinishHotkeyCapture();
        ClearQuickUseFeedback();
        return true;
    }

    internal bool SavePendingQuickUseCommands()
    {
        FinishHotkeyCapture();
        var pending = quickUseRows.Select(row => row.ToSetting()).ToArray();
        if (!leagueSetting.TrySaveQuickUseCommands(pending, out var validationError))
        {
            ShowQuickUseError(validationError);
            return false;
        }

        RestoreQuickUseCommands();
        QuickUseFeedbackText.Text = "Quick Use settings saved.";
        QuickUseFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(99, 212, 113));
        return true;
    }

    internal void CloseForApplicationExit()
    {
        allowApplicationClose = true;
        Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (capturingQuickUseRow is not null)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            _ = CapturePendingHotkey(key, Keyboard.Modifiers);
            e.Handled = true;
            return;
        }

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

    private void StartNavigationButton_OnClick(object sender, RoutedEventArgs e)
    {
        ShowStartView();
    }

    private void SettingsNavigationButton_OnClick(object sender, RoutedEventArgs e)
    {
        ShowSettingsView();
    }

    private void LeagueComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CustomLeagueTextBox.IsEnabled = SelectedLeagueChoice() == OtherLeagueChoice;
        ClearLeagueFeedback();
    }

    private void CustomLeagueTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ClearLeagueFeedback();
    }

    private void ApplyLeagueButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = ApplyPendingLeague();
    }

    private void AddQuickUseCommandButton_OnClick(object sender, RoutedEventArgs e)
    {
        AddPendingQuickUseCommand();
    }

    private void RemoveQuickUseCommandButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QuickUseCommandRow row })
        {
            _ = RemovePendingQuickUseCommand(quickUseRows.IndexOf(row));
        }
    }

    private void QuickUseHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QuickUseCommandRow row })
        {
            BeginHotkeyCapture(quickUseRows.IndexOf(row));
        }
    }

    private void QuickUsePendingValue_OnChanged(object sender, RoutedEventArgs e)
    {
        ClearQuickUseFeedback();
    }

    private void QuickUseCommandTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ClearQuickUseFeedback();
    }

    private void SaveQuickUseButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = SavePendingQuickUseCommands();
    }

    private void RestoreLeagueSelection()
    {
        isRestoringLeagueSelection = true;
        try
        {
            var effectiveLeague = leagueSetting.EffectiveLeague;
            if (effectiveLeague is null)
            {
                SelectPendingLeague(SelectLeaguePlaceholder);
                return;
            }

            if (BuiltInLeagueChoices.Contains(effectiveLeague, StringComparer.Ordinal))
            {
                SelectPendingLeague(effectiveLeague);
                return;
            }

            SelectPendingLeague(OtherLeagueChoice);
            CustomLeagueTextBox.Text = effectiveLeague;
        }
        finally
        {
            isRestoringLeagueSelection = false;
            ClearLeagueFeedback();
        }
    }

    private void RestoreQuickUseCommands()
    {
        CancelHotkeyCapture();
        quickUseRows.Clear();
        foreach (var command in leagueSetting.QuickUseCommands)
        {
            quickUseRows.Add(new QuickUseCommandRow(command));
        }
    }

    private string? SelectedLeagueChoice()
    {
        return (LeagueComboBox.SelectedItem as ComboBoxItem)?.Content as string;
    }

    private void ClearLeagueFeedback()
    {
        if (!isRestoringLeagueSelection)
        {
            LeagueFeedbackText.Text = string.Empty;
        }
    }

    private void ShowLeagueError(string message)
    {
        LeagueFeedbackText.Text = message;
        LeagueFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
    }

    private void ClearQuickUseFeedback()
    {
        if (QuickUseFeedbackText is not null)
        {
            QuickUseFeedbackText.Text = string.Empty;
        }
    }

    private void ShowQuickUseError(string message)
    {
        QuickUseFeedbackText.Text = message;
        QuickUseFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
    }

    private void CancelHotkeyCapture()
    {
        if (capturingQuickUseRow is null)
        {
            return;
        }

        capturingQuickUseRow.Hotkey = hotkeyBeforeCapture;
        FinishHotkeyCapture();
    }

    private void FinishHotkeyCapture()
    {
        if (capturingQuickUseRow is null)
        {
            return;
        }

        capturingQuickUseRow.IsCapturing = false;
        capturingQuickUseRow = null;
        hotkeyBeforeCapture = null;
        HotkeyCaptureStateChanged?.Invoke(this, false);
    }

    private static bool TryCreateShortcutBinding(
        Key key,
        ModifierKeys modifiers,
        out ShortcutBinding binding)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (!Enum.IsDefined(typeof(ShortcutKey), virtualKey))
        {
            binding = null!;
            return false;
        }

        var shortcutModifiers = ShortcutModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            shortcutModifiers |= ShortcutModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            shortcutModifiers |= ShortcutModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            shortcutModifiers |= ShortcutModifiers.Shift;
        }

        binding = new ShortcutBinding((ShortcutKey)virtualKey, shortcutModifiers);
        return true;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
    }

    private void HideForReuse()
    {
        CancelHotkeyCapture();
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

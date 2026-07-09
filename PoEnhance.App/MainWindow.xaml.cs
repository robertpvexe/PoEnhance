using System.Windows;
using System.Windows.Threading;
using PoEnhance.App.Infrastructure.Clipboard;
using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Shortcuts;
using Serilog;

namespace PoEnhance.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly GlobalHotkeyService globalHotkeyService = new();
    private readonly PathOfExileForegroundWindowDetector pathOfExileForegroundWindowDetector = new();
    private readonly PathOfExileProcessDetector pathOfExileProcessDetector = new();
    private readonly WpfClipboardTextReader clipboardTextReader = new();
    private readonly DispatcherTimer pathOfExileStatusTimer;
    private string? rawClipboardText;
    private int shortcutActivationCount;
    private bool? lastPathOfExileForeground;
    private bool? lastPathOfExileRunning;

    public MainWindow()
    {
        InitializeComponent();
        InitializeShortcutControls();

        pathOfExileStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        pathOfExileStatusTimer.Tick += (_, _) => RefreshPathOfExileStatus();
        globalHotkeyService.Triggered += OnShortcutTriggered;

        SourceInitialized += (_, _) => globalHotkeyService.Attach(this);
        Loaded += (_, _) =>
        {
            RefreshPathOfExileStatus();
            pathOfExileStatusTimer.Start();
        };

        Closed += (_, _) =>
        {
            pathOfExileStatusTimer.Stop();
            globalHotkeyService.Dispose();
        };
    }

    private void InitializeShortcutControls()
    {
        ShortcutComboBox.ItemsSource = Enum.GetValues<ShortcutKey>();
        ShortcutComboBox.SelectedItem = ShortcutKey.X;
        ShortcutComboBox.SelectionChanged += (_, _) =>
        {
            if (ShortcutComboBox.SelectedItem is ShortcutKey shortcut)
            {
                globalHotkeyService.SetShortcut(shortcut);
                UpdateShortcutRegistrationStatus();
            }
        };
    }

    private void RefreshPathOfExileStatus()
    {
        bool isRunning = pathOfExileProcessDetector.IsPathOfExileRunning();
        bool isForeground = pathOfExileForegroundWindowDetector.IsPathOfExileForegroundWindow();

        PathOfExileStatusText.Text = isRunning
            ? "Path of Exile: Running"
            : "Path of Exile: Not running";
        ForegroundWindowStatusText.Text = isForeground
            ? "Foreground window: Path of Exile"
            : "Foreground window: Other application";

        LogPathOfExileRunningChange(isRunning);
        LogPathOfExileForegroundChange(isForeground);

        globalHotkeyService.UpdatePathOfExileForegroundState(isForeground);
        UpdateShortcutRegistrationStatus();
    }

    private void LogPathOfExileRunningChange(bool isRunning)
    {
        if (lastPathOfExileRunning == isRunning)
        {
            return;
        }

        if (isRunning)
        {
            Log.Information("Path of Exile detected");
        }
        else if (lastPathOfExileRunning is true)
        {
            Log.Information("Path of Exile no longer detected");
        }

        lastPathOfExileRunning = isRunning;
    }

    private void LogPathOfExileForegroundChange(bool isForeground)
    {
        if (lastPathOfExileForeground == isForeground)
        {
            return;
        }

        if (isForeground)
        {
            Log.Information("Path of Exile became the foreground application");
        }
        else if (lastPathOfExileForeground is true)
        {
            Log.Information("Path of Exile is no longer the foreground application");
        }

        lastPathOfExileForeground = isForeground;
    }

    private void OnShortcutTriggered(object? sender, EventArgs e)
    {
        shortcutActivationCount++;
        ShortcutActivationStatusText.Text =
            $"Shortcut activations: {shortcutActivationCount} (last: {DateTimeOffset.Now:HH:mm:ss})";

        Log.Information(
            "Shortcut {ShortcutKey} triggered while Path of Exile is foreground",
            globalHotkeyService.SelectedShortcut);

        CaptureClipboardText();
    }

    private void CaptureClipboardText()
    {
        ClipboardTextReadResult result = clipboardTextReader.ReadText();

        switch (result.Status)
        {
            case ClipboardTextReadStatus.TextAvailable:
                rawClipboardText = result.Text;
                RawClipboardTextBox.Text = rawClipboardText;
                ClipboardCaptureStatusText.Text =
                    $"Clipboard: Captured {rawClipboardText?.Length ?? 0} characters";
                break;

            case ClipboardTextReadStatus.EmptyOrNoText:
                rawClipboardText = null;
                RawClipboardTextBox.Clear();
                ClipboardCaptureStatusText.Text = "Clipboard: Empty or does not contain text";
                break;

            case ClipboardTextReadStatus.AccessFailed:
                rawClipboardText = null;
                RawClipboardTextBox.Clear();
                ClipboardCaptureStatusText.Text = "Clipboard: Temporarily unavailable";
                LogClipboardAccessFailure(result.Exception);
                break;
        }
    }

    private static void LogClipboardAccessFailure(Exception? exception)
    {
        if (exception is null)
        {
            Log.Warning("Clipboard text capture failed");
            return;
        }

        Log.Warning(
            "Clipboard text capture failed. {ExceptionType}: {ExceptionMessage}",
            exception.GetType().FullName,
            exception.Message);
    }

    private void UpdateShortcutRegistrationStatus()
    {
        ShortcutRegistrationStatusText.Text = globalHotkeyService.RegistrationState switch
        {
            ShortcutRegistrationState.Active => "Shortcut registration: Active",
            ShortcutRegistrationState.RegistrationFailed => "Shortcut registration: Registration failed",
            _ => "Shortcut registration: Inactive because Path of Exile is not foreground",
        };
    }
}

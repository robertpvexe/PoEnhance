using System.Windows;
using System.Windows.Threading;
using PoEnhance.App.Infrastructure.Clipboard;
using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Shortcuts;
using PoEnhance.Core.Items.Parsing;
using Serilog;

namespace PoEnhance.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string NotDetectedText = "Not detected";

    private readonly GlobalHotkeyService globalHotkeyService = new();
    private readonly ItemTextParser itemTextParser = new();
    private readonly PathOfExileForegroundWindowDetector pathOfExileForegroundWindowDetector = new();
    private readonly PathOfExileProcessDetector pathOfExileProcessDetector = new();
    private readonly WpfClipboardTextReader clipboardTextReader = new();
    private readonly DispatcherTimer pathOfExileStatusTimer;
    private string? rawClipboardText;
    private string? rawManualItemText;
    private int shortcutActivationCount;
    private bool? lastPathOfExileForeground;
    private bool? lastPathOfExileRunning;

    public MainWindow()
    {
        InitializeComponent();
        InitializeShortcutControls();
        InitializeManualInputControls();
        ClearParsedItemResult();

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

    private void InitializeManualInputControls()
    {
        ParseManualInputButton.Click += (_, _) => ParseManualItemInput();
        ClearManualInputButton.Click += (_, _) => ClearManualItemInput();
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
                rawClipboardText = result.Text ?? string.Empty;
                RawClipboardTextBox.Text = rawClipboardText;
                ParseRawItemText(rawClipboardText, ItemInputSource.Clipboard);
                break;

            case ClipboardTextReadStatus.EmptyOrNoText:
                rawClipboardText = null;
                RawClipboardTextBox.Clear();
                ClearParsedItemResult();
                ClipboardCaptureStatusText.Text = "Clipboard: Empty or does not contain text";
                break;

            case ClipboardTextReadStatus.AccessFailed:
                rawClipboardText = null;
                RawClipboardTextBox.Clear();
                ClearParsedItemResult();
                ClipboardCaptureStatusText.Text = "Clipboard: Temporarily unavailable";
                LogClipboardAccessFailure(result.Exception);
                break;
        }
    }

    private void ParseManualItemInput()
    {
        rawManualItemText = ManualItemInputTextBox.Text ?? string.Empty;
        ParseRawItemText(rawManualItemText, ItemInputSource.Manual);
    }

    private void ClearManualItemInput()
    {
        rawManualItemText = null;
        ManualItemInputTextBox.Clear();
        ClearParsedItemResult();
        ManualInputStatusText.Text = "Manual input: Cleared";
    }

    private void ParseRawItemText(string rawText, ItemInputSource inputSource)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            ClearParsedItemResult();
            SetInputStatus(inputSource, "Empty or whitespace-only text");
            return;
        }

        try
        {
            ParsedItem parsedItem = itemTextParser.Parse(rawText);
            DisplayParsedItemResult(parsedItem);
            SetInputStatus(inputSource, $"Parsed {rawText.Length} characters");
        }
        catch (Exception exception)
        {
            ClearParsedItemResult();
            SetInputStatus(inputSource, "Item parsing failed");
            LogItemParsingFailure(inputSource, exception);
        }
    }

    private void SetInputStatus(ItemInputSource inputSource, string message)
    {
        switch (inputSource)
        {
            case ItemInputSource.Clipboard:
                ClipboardCaptureStatusText.Text = $"Clipboard: {message}";
                break;

            case ItemInputSource.Manual:
                ManualInputStatusText.Text = $"Manual input: {message}";
                break;
        }
    }

    private void DisplayParsedItemResult(ParsedItem parsedItem)
    {
        ParsedItemClassText.Text = DisplayValue(parsedItem.ItemClass);
        ParsedRarityText.Text = DisplayValue(parsedItem.Rarity);
        ParsedNameText.Text = DisplayValue(parsedItem.Name);
        ParsedBaseTypeText.Text = DisplayValue(parsedItem.BaseType);
        ParsedItemLevelText.Text = parsedItem.ItemLevel?.ToString() ?? NotDetectedText;
        ParsedPropertiesTextBox.Text = DisplayLines(parsedItem.PropertyLines);
        ParsedModifiersTextBox.Text = DisplayLines(parsedItem.ModifierLines);
        ParsedUnclassifiedTextBox.Text = DisplayLines(parsedItem.UnclassifiedLines);
    }

    private void ClearParsedItemResult()
    {
        ParsedItemClassText.Text = NotDetectedText;
        ParsedRarityText.Text = NotDetectedText;
        ParsedNameText.Text = NotDetectedText;
        ParsedBaseTypeText.Text = NotDetectedText;
        ParsedItemLevelText.Text = NotDetectedText;
        ParsedPropertiesTextBox.Text = NotDetectedText;
        ParsedModifiersTextBox.Text = NotDetectedText;
        ParsedUnclassifiedTextBox.Text = NotDetectedText;
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? NotDetectedText : value;
    }

    private static string DisplayLines(IReadOnlyCollection<string> lines)
    {
        return lines.Count == 0 ? NotDetectedText : string.Join(Environment.NewLine, lines);
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

    private static void LogItemParsingFailure(ItemInputSource inputSource, Exception exception)
    {
        Log.Warning(
            "{InputSource} item parsing failed. {ExceptionType}: {ExceptionMessage}",
            inputSource,
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

    private enum ItemInputSource
    {
        Clipboard,
        Manual,
    }
}

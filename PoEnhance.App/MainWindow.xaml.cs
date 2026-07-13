using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Clipboard;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Input;
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
    private static readonly TimeSpan CopyChordDelay = TimeSpan.FromMilliseconds(75);
    private static readonly TimeSpan ClipboardCaptureTimeout = TimeSpan.FromMilliseconds(650);

    private readonly ClipboardSequenceNumberReader clipboardSequenceNumberReader = new();
    private readonly GlobalHotkeyService globalHotkeyService = new();
    private readonly ItemTextParser itemTextParser = new();
    private readonly ParsedItemGameDataDisplayService itemGameDataDisplayService = new();
    private readonly KeyboardInputSender keyboardInputSender = new();
    private readonly PathOfExileForegroundWindowDetector pathOfExileForegroundWindowDetector = new();
    private readonly PathOfExileProcessDetector pathOfExileProcessDetector = new();
    private readonly PriceCheckerWindowController priceCheckerWindowController;
    private readonly ProvisionalGameDataRecordingService provisionalGameDataRecordingService;
    private readonly RuntimeGameDataService runtimeGameDataService;
    private readonly WpfClipboardTextReader clipboardTextReader = new();
    private readonly DispatcherTimer pathOfExileStatusTimer;
    private int shortcutActivationCount;
    private bool isClipboardCaptureInProgress;
    private bool? lastPathOfExileForeground;
    private bool? lastPathOfExileRunning;

    public MainWindow()
        : this(new RuntimeGameDataService())
    {
    }

    internal MainWindow(RuntimeGameDataService runtimeGameDataService)
        : this(
            runtimeGameDataService,
            new ProvisionalGameDataRecordingService(
                new JsonProvisionalGameDataStore(
                    new ProvisionalGameDataStorePathResolver().ResolveDefaultPath())))
    {
    }

    internal MainWindow(
        RuntimeGameDataService runtimeGameDataService,
        ProvisionalGameDataRecordingService provisionalGameDataRecordingService)
    {
        this.runtimeGameDataService = runtimeGameDataService;
        this.provisionalGameDataRecordingService = provisionalGameDataRecordingService;

        InitializeComponent();
        priceCheckerWindowController = new PriceCheckerWindowController(
            new PriceCheckerWindowFactory());
        InitializeShortcutControls();
        InitializeManualInputControls();
        DisplayRuntimeGameDataStatus(runtimeGameDataService.Current);
        DisplayProvisionalStoreStatus(provisionalGameDataRecordingService.Status);
        ClearParsedItemResult();

        pathOfExileStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        pathOfExileStatusTimer.Tick += (_, _) => RefreshPathOfExileStatus();
        globalHotkeyService.Triggered += OnShortcutTriggered;
        runtimeGameDataService.StateChanged += OnRuntimeGameDataStateChanged;

        SourceInitialized += (_, _) => globalHotkeyService.Attach(this);
        Loaded += (_, _) =>
        {
            RefreshPathOfExileStatus();
            pathOfExileStatusTimer.Start();
            _ = RefreshProvisionalStoreStatusAsync();
        };

        Closed += (_, _) =>
        {
            pathOfExileStatusTimer.Stop();
            runtimeGameDataService.StateChanged -= OnRuntimeGameDataStateChanged;
            globalHotkeyService.Dispose();
        };
    }

    internal async Task LoadGameDataAsync(
        IReadOnlyList<string> commandLineArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            await runtimeGameDataService.LoadAsync(commandLineArgs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Runtime game-data loading canceled during shutdown");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Unexpected runtime game-data startup failure");
        }
    }

    private void InitializeManualInputControls()
    {
        ParseManualInputButton.Click += async (_, _) => await ParseManualItemInputAsync();
        ClearManualInputButton.Click += (_, _) => ClearManualItemInput();
    }

    private void InitializeShortcutControls()
    {
        ShortcutComboBox.ItemsSource = ShortcutBinding.DevelopmentChoices;
        ShortcutComboBox.SelectedItem = ShortcutBinding.DefaultPriceChecker;
        ShortcutComboBox.SelectionChanged += (_, _) =>
        {
            if (ShortcutComboBox.SelectedItem is ShortcutBinding shortcut)
            {
                globalHotkeyService.SetShortcut(shortcut);
                UpdateShortcutRegistrationStatus();
            }
        };
    }

    private void OnRuntimeGameDataStateChanged(object? sender, RuntimeGameDataStatus status)
    {
        if (Dispatcher.CheckAccess())
        {
            DisplayRuntimeGameDataStatus(status);
            return;
        }

        _ = Dispatcher.BeginInvoke(() => DisplayRuntimeGameDataStatus(status));
    }

    private void DisplayRuntimeGameDataStatus(RuntimeGameDataStatus status)
    {
        GameDataStateText.Text = status.State switch
        {
            RuntimeGameDataState.NotConfigured => "Not loaded",
            RuntimeGameDataState.Loading => "Loading",
            RuntimeGameDataState.Loaded => "Loaded",
            RuntimeGameDataState.Failed => "Failed",
            _ => "Not loaded",
        };
        GameDataPathText.Text = ShortenPath(status.PackagePath);
        GameDataVersionText.Text = DisplayValue(status.DataVersion);
        GameDataSourceVersionText.Text = DisplayValue(status.SourceVersion);
        GameDataRecordCountsText.Text = status.State == RuntimeGameDataState.Loaded
            ? $"ItemBases: {status.ItemBaseCount}, Modifiers: {status.ModifierCount}, Stats: {status.StatCount}, StatTranslations: {status.StatTranslationCount}"
            : NotDetectedText;
        GameDataDiagnosticText.Text = DisplayRuntimeGameDataDiagnostic(status);
    }

    private static string DisplayRuntimeGameDataDiagnostic(RuntimeGameDataStatus status)
    {
        var diagnostic = status.Diagnostics.FirstOrDefault();
        if (diagnostic is not null)
        {
            return $"{diagnostic.Code}: {diagnostic.Message}";
        }

        var validationError = status.ValidationErrors.FirstOrDefault();
        if (validationError is not null)
        {
            return $"{validationError.Code}: {validationError.Message}";
        }

        return DisplayValue(status.FailureMessage);
    }

    private void DisplayProvisionalStoreStatus(ProvisionalGameDataStoreStatus status)
    {
        ProvisionalStoreStateText.Text = $"Records: {status.RecordCount}";
        ProvisionalStorePathText.Text = ShortenPath(status.FilePath);
        ProvisionalStoreDiagnosticText.Text = DisplayValue(status.LastDiagnostic);
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

    private async void OnShortcutTriggered(object? sender, EventArgs e)
    {
        shortcutActivationCount++;
        ShortcutActivationStatusText.Text =
            $"Shortcut activations: {shortcutActivationCount} (last: {DateTimeOffset.Now:HH:mm:ss})";

        Log.Information(
            "Shortcut {ShortcutKey} triggered while Path of Exile is foreground",
            globalHotkeyService.SelectedShortcut);

        await CaptureItemTextFromPathOfExileAsync();
    }

    private async Task CaptureItemTextFromPathOfExileAsync()
    {
        if (isClipboardCaptureInProgress)
        {
            ClipboardCaptureStatusText.Text = "Clipboard: Capture already in progress";
            Log.Warning("Item capture skipped because a previous capture is still in progress");
            return;
        }

        isClipboardCaptureInProgress = true;

        try
        {
            if (!pathOfExileForegroundWindowDetector.IsPathOfExileForegroundWindow())
            {
                InvalidateClipboardCapture("Path of Exile is not foreground");
                Log.Warning("Item capture canceled because Path of Exile is not foreground");
                return;
            }

            await Task.Delay(CopyChordDelay);

            if (!pathOfExileForegroundWindowDetector.IsPathOfExileForegroundWindow())
            {
                InvalidateClipboardCapture("Path of Exile lost foreground before copy");
                Log.Warning("Item capture canceled because Path of Exile lost foreground before Ctrl+Alt+C was sent");
                return;
            }

            uint sequenceNumberBeforeCopy = clipboardSequenceNumberReader.GetCurrentSequenceNumber();
            if (!keyboardInputSender.TrySendAdvancedItemDescriptionCopyChord(out uint sentInputCount, out int errorCode))
            {
                InvalidateClipboardCapture("Copy input failed");
                Log.Warning(
                    "Ctrl+Alt+C SendInput failed. Sent input count: {SentInputCount}. Win32 error: {Win32ErrorCode}",
                    sentInputCount,
                    errorCode);
                return;
            }

            ClipboardCaptureWaitResult waitResult =
                await WaitForClipboardSequenceChangeAsync(sequenceNumberBeforeCopy);
            if (waitResult == ClipboardCaptureWaitResult.ForegroundLost)
            {
                InvalidateClipboardCapture("Path of Exile lost foreground during capture");
                Log.Warning("Item capture canceled because Path of Exile lost foreground during capture");
                return;
            }

            if (waitResult == ClipboardCaptureWaitResult.TimedOut)
            {
                InvalidateClipboardCapture("No item text was copied");
                Log.Information("Item capture timed out before the clipboard changed");
                return;
            }

            if (!pathOfExileForegroundWindowDetector.IsPathOfExileForegroundWindow())
            {
                InvalidateClipboardCapture("Path of Exile lost foreground during capture");
                Log.Warning("Item capture canceled because Path of Exile lost foreground during capture");
                return;
            }

            await ReadClipboardTextAfterCaptureAsync();
        }
        finally
        {
            isClipboardCaptureInProgress = false;
        }
    }

    private async Task<ClipboardCaptureWaitResult> WaitForClipboardSequenceChangeAsync(uint initialSequenceNumber)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + ClipboardCaptureTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(25);

            if (!pathOfExileForegroundWindowDetector.IsPathOfExileForegroundWindow())
            {
                return ClipboardCaptureWaitResult.ForegroundLost;
            }

            uint currentSequenceNumber = clipboardSequenceNumberReader.GetCurrentSequenceNumber();
            if (currentSequenceNumber != initialSequenceNumber)
            {
                return ClipboardCaptureWaitResult.ClipboardChanged;
            }
        }

        return ClipboardCaptureWaitResult.TimedOut;
    }

    private void InvalidateClipboardCapture(string status)
    {
        ClearRawInputText();
        ClearParsedItemResult();
        ClipboardCaptureStatusText.Text = $"Clipboard: {status}";
    }

    private async Task ReadClipboardTextAfterCaptureAsync()
    {
        ClipboardTextReadResult result = clipboardTextReader.ReadText();

        switch (result.Status)
        {
            case ClipboardTextReadStatus.TextAvailable:
                var rawClipboardText = result.Text ?? string.Empty;
                DisplayRawInputText(rawClipboardText);
                await ParseRawItemTextAsync(rawClipboardText, ItemInputSource.Clipboard);
                Log.Information("Item capture succeeded from clipboard text");
                break;

            case ClipboardTextReadStatus.EmptyOrNoText:
                InvalidateClipboardCapture("Empty or does not contain text");
                Log.Information("Item capture completed but clipboard did not contain text");
                break;

            case ClipboardTextReadStatus.AccessFailed:
                InvalidateClipboardCapture("Temporarily unavailable");
                LogClipboardAccessFailure(result.Exception);
                break;
        }
    }

    private async Task ParseManualItemInputAsync()
    {
        var rawManualItemText = ManualItemInputTextBox.Text ?? string.Empty;
        DisplayRawInputText(rawManualItemText);
        await ParseRawItemTextAsync(rawManualItemText, ItemInputSource.Manual);
    }

    private void ClearManualItemInput()
    {
        ManualItemInputTextBox.Clear();
        ClearRawInputText();
        ClearParsedItemResult();
        ManualInputStatusText.Text = "Manual input: Cleared";
    }

    private void DisplayRawInputText(string rawText)
    {
        RawInputTextBox.Text = rawText;
    }

    private void ClearRawInputText()
    {
        RawInputTextBox.Clear();
    }

    private async Task ParseRawItemTextAsync(string rawText, ItemInputSource inputSource)
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
            var itemBaseResolution = itemGameDataDisplayService.ResolveItemBase(
                parsedItem,
                runtimeGameDataService.Current.Catalog);
            var modifierCandidateResolutions = itemGameDataDisplayService.ResolveModifierCandidates(
                parsedItem,
                runtimeGameDataService.Current.Catalog,
                itemBaseResolution.Result);
            DisplayParsedItemResult(parsedItem, itemBaseResolution, modifierCandidateResolutions);
            var priceCheckerUpdateResult = inputSource == ItemInputSource.Clipboard
                ? ShowOrUpdatePriceCheckerWindow(
                    parsedItem,
                    itemBaseResolution,
                    modifierCandidateResolutions)
                : null;
            if (itemBaseResolution.Result is not null)
            {
                await RecordProvisionalGameDataAsync(
                    parsedItem,
                    itemBaseResolution.Result,
                    modifierCandidateResolutions,
                    CreateProcessingEventKey(inputSource));
            }

            var status = $"Parsed {rawText.Length} characters";
            if (priceCheckerUpdateResult is not null)
            {
                status = $"{status}; {priceCheckerUpdateResult.Diagnostic}";
            }

            SetInputStatus(inputSource, status);
        }
        catch (Exception exception)
        {
            ClearParsedItemResult();
            SetInputStatus(inputSource, "Item parsing failed");
            LogItemParsingFailure(inputSource, exception);
        }
    }

    private static string CreateProcessingEventKey(ItemInputSource inputSource)
    {
        return $"{inputSource}:{Guid.NewGuid():N}";
    }

    private PriceCheckerWindowUpdateResult ShowOrUpdatePriceCheckerWindow(
        ParsedItem parsedItem,
        ItemBaseResolutionDisplay itemBaseResolution,
        ModifierCandidateResolutionsDisplay modifierCandidateResolutions)
    {
        var result = priceCheckerWindowController.ShowOrUpdate(
            parsedItem,
            itemBaseResolution.Result,
            modifierCandidateResolutions.Results
                .Select(display => display.Result)
                .OfType<PoEnhance.Core.Items.GameData.ModifierCandidateResolutionResult>()
                .ToArray());

        if (!result.IsSuccess)
        {
            Log.Warning("Price Checker window was not updated. {Diagnostic}", result.Diagnostic);
        }

        return result;
    }

    private async Task RefreshProvisionalStoreStatusAsync()
    {
        await provisionalGameDataRecordingService.LoadSnapshotAsync();
        await Dispatcher.BeginInvoke(() => DisplayProvisionalStoreStatus(provisionalGameDataRecordingService.Status));
    }

    private async Task RecordProvisionalGameDataAsync(
        ParsedItem parsedItem,
        PoEnhance.Core.Items.GameData.ItemBaseResolutionResult itemBaseResolution,
        ModifierCandidateResolutionsDisplay modifierCandidateResolutions,
        string processingEventKey)
    {
        try
        {
            var result = await provisionalGameDataRecordingService.RecordAsync(
                parsedItem,
                runtimeGameDataService.Current,
                itemBaseResolution,
                modifierCandidateResolutions.Results.Select(display => display.Result).OfType<PoEnhance.Core.Items.GameData.ModifierCandidateResolutionResult>().ToArray(),
                processingEventKey);

            await Dispatcher.BeginInvoke(() => DisplayProvisionalStoreStatus(result.StoreStatus));
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Provisional game-data recording failed without interrupting item parsing");
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

    private void DisplayParsedItemResult(
        ParsedItem parsedItem,
        ItemBaseResolutionDisplay itemBaseResolution,
        ModifierCandidateResolutionsDisplay modifierCandidateResolutions)
    {
        ParsedInputFormatText.Text = parsedItem.InputFormat.ToString();
        ParsedItemClassText.Text = DisplayValue(parsedItem.ItemClass);
        ParsedRarityText.Text = DisplayValue(parsedItem.Rarity);
        ParsedNameText.Text = DisplayValue(parsedItem.DisplayName);
        ParsedBaseTypeText.Text = DisplayValue(parsedItem.BaseType);
        DisplayItemTypeDescriptor(parsedItem.ItemTypeDescriptor);
        DisplayItemStates(parsedItem.ItemStates);
        DisplayNoteLines(parsedItem.NoteLines);
        ParsedItemLevelText.Text = parsedItem.ItemLevel?.ToString() ?? NotDetectedText;
        DisplayItemBaseResolution(itemBaseResolution);
        ParsedPropertiesTextBox.Text = DisplayLines(parsedItem.PropertyLines);
        DisplayOptionalTextBox(
            ParsedInfluencesPanel,
            ParsedInfluencesTextBox,
            DisplayInfluences(parsedItem.TraditionalInfluences, parsedItem.EldritchInfluences));
        DisplayOptionalTextBox(
            ParsedImplicitModifiersPanel,
            ParsedImplicitModifiersTextBox,
            DisplayModifiers(parsedItem.ImplicitModifiers, modifierCandidateResolutions));
        DisplayOptionalTextBox(
            ParsedPrefixModifiersPanel,
            ParsedPrefixModifiersTextBox,
            DisplayModifiers(parsedItem.PrefixModifiers, modifierCandidateResolutions));
        DisplayOptionalTextBox(
            ParsedSuffixModifiersPanel,
            ParsedSuffixModifiersTextBox,
            DisplayModifiers(parsedItem.SuffixModifiers, modifierCandidateResolutions));
        DisplayOptionalTextBox(
            ParsedUniqueModifiersPanel,
            ParsedUniqueModifiersTextBox,
            DisplayModifiers(parsedItem.UniqueModifiers, modifierCandidateResolutions));
        DisplayOptionalTextBox(
            ParsedUnknownModifiersPanel,
            ParsedUnknownModifiersTextBox,
            DisplayModifiers(parsedItem.ExplicitModifiersWithUnknownKind, modifierCandidateResolutions));
        DisplayOptionalTextBox(
            ParsedEnchantmentsPanel,
            ParsedEnchantmentsTextBox,
            DisplayEnchantments(parsedItem.Enchantments));
        DisplayOptionalTextBox(
            ParsedFlavourTextPanel,
            ParsedFlavourTextBox,
            DisplayLines(parsedItem.FlavourTextLines));
        DisplayOptionalTextBox(
            ParsedListingNotePanel,
            ParsedListingNoteTextBox,
            DisplayValue(parsedItem.ListingNote));
        DisplayOptionalTextBox(
            ParsedUnclassifiedPanel,
            ParsedUnclassifiedTextBox,
            DisplayLines(parsedItem.UnclassifiedLines));
    }

    private void ClearParsedItemResult()
    {
        ParsedInputFormatText.Text = NotDetectedText;
        ParsedItemClassText.Text = NotDetectedText;
        ParsedRarityText.Text = NotDetectedText;
        ParsedNameText.Text = NotDetectedText;
        ParsedBaseTypeText.Text = NotDetectedText;
        DisplayItemTypeDescriptor(null);
        DisplayItemStates([]);
        DisplayNoteLines([]);
        ParsedItemLevelText.Text = NotDetectedText;
        ParsedItemBaseResolutionTextBox.Text = NotDetectedText;
        ParsedPropertiesTextBox.Text = NotDetectedText;
        DisplayOptionalTextBox(ParsedInfluencesPanel, ParsedInfluencesTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedImplicitModifiersPanel, ParsedImplicitModifiersTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedPrefixModifiersPanel, ParsedPrefixModifiersTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedSuffixModifiersPanel, ParsedSuffixModifiersTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedUniqueModifiersPanel, ParsedUniqueModifiersTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedUnknownModifiersPanel, ParsedUnknownModifiersTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedEnchantmentsPanel, ParsedEnchantmentsTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedFlavourTextPanel, ParsedFlavourTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedListingNotePanel, ParsedListingNoteTextBox, NotDetectedText);
        DisplayOptionalTextBox(ParsedUnclassifiedPanel, ParsedUnclassifiedTextBox, NotDetectedText);
    }

    private void DisplayItemBaseResolution(ItemBaseResolutionDisplay resolution)
    {
        var lines = new List<string>
        {
            $"Status: {resolution.Status}",
            $"Resolved base name: {resolution.ResolvedBaseName}",
            $"Resolved base ID: {resolution.ResolvedBaseId}",
            $"Diagnostic: {resolution.Diagnostic}",
            $"Candidate count: {resolution.CandidateCount}",
        };

        if (resolution.CandidateNames.Count > 0)
        {
            lines.Add($"Candidate names: {string.Join(", ", resolution.CandidateNames)}");
        }

        ParsedItemBaseResolutionTextBox.Text = string.Join(Environment.NewLine, lines);
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? NotDetectedText : value;
    }

    private static string ShortenPath(string? path)
    {
        const int maxLength = 96;

        if (string.IsNullOrWhiteSpace(path))
        {
            return NotDetectedText;
        }

        if (path.Length <= maxLength)
        {
            return path;
        }

        var fileName = System.IO.Path.GetFileName(path);
        var directoryName = System.IO.Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(directoryName))
        {
            return ShortenFromEnd(path, maxLength);
        }

        var suffix = System.IO.Path.Combine(
            System.IO.Path.GetFileName(directoryName),
            fileName);
        return suffix.Length + 4 >= maxLength
            ? ShortenFromEnd(path, maxLength)
            : $"...\\{suffix}";
    }

    private static string ShortenFromEnd(string value, int maxLength)
    {
        var retainedLength = Math.Min(value.Length, maxLength - 3);
        return $"...{value.Substring(value.Length - retainedLength, retainedLength)}";
    }

    private void DisplayItemTypeDescriptor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ParsedItemTypeDescriptorLabel.Visibility = Visibility.Collapsed;
            ParsedItemTypeDescriptorText.Visibility = Visibility.Collapsed;
            ParsedItemTypeDescriptorText.Text = NotDetectedText;
            return;
        }

        ParsedItemTypeDescriptorLabel.Visibility = Visibility.Visible;
        ParsedItemTypeDescriptorText.Visibility = Visibility.Visible;
        ParsedItemTypeDescriptorText.Text = value;
    }

    private void DisplayItemStates(IReadOnlyCollection<string> states)
    {
        DisplayOptionalLines(ParsedItemStatesLabel, ParsedItemStatesText, states);
    }

    private void DisplayNoteLines(IReadOnlyCollection<string> noteLines)
    {
        DisplayOptionalLines(ParsedNoteLinesLabel, ParsedNoteLinesText, noteLines);
    }

    private static void DisplayOptionalLines(
        UIElement label,
        TextBlock textBlock,
        IReadOnlyCollection<string> lines)
    {
        if (lines.Count == 0)
        {
            label.Visibility = Visibility.Collapsed;
            textBlock.Visibility = Visibility.Collapsed;
            textBlock.Text = NotDetectedText;
            return;
        }

        label.Visibility = Visibility.Visible;
        textBlock.Visibility = Visibility.Visible;
        textBlock.Text = DisplayLines(lines);
    }

    private static string DisplayLines(IReadOnlyCollection<string> lines)
    {
        return lines.Count == 0 ? NotDetectedText : string.Join(Environment.NewLine, lines);
    }

    private static string DisplayInfluences(
        IReadOnlyCollection<string> traditionalInfluences,
        IReadOnlyCollection<string> eldritchInfluences)
    {
        var lines = new List<string>();
        lines.AddRange(traditionalInfluences.Select(influence => $"Traditional: {influence}"));
        lines.AddRange(eldritchInfluences.Select(influence => $"Eldritch: {influence}"));

        return DisplayLines(lines);
    }

    private static string DisplayEnchantments(IReadOnlyCollection<ParsedEnchantment> enchantments)
    {
        return enchantments.Count == 0
            ? NotDetectedText
            : string.Join(Environment.NewLine, enchantments.Select(enchantment =>
                enchantment.IsAnoint ? $"{enchantment.Text} [Anoint]" : enchantment.Text));
    }

    private static void DisplayOptionalTextBox(FrameworkElement panel, TextBox textBox, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == NotDetectedText)
        {
            panel.Visibility = Visibility.Collapsed;
            textBox.Text = NotDetectedText;
            return;
        }

        panel.Visibility = Visibility.Visible;
        textBox.Text = text;
    }

    private static string DisplayModifiers(
        IReadOnlyCollection<ParsedModifier> modifiers,
        ModifierCandidateResolutionsDisplay modifierCandidateResolutions)
    {
        return modifiers.Count == 0
            ? NotDetectedText
            : string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                modifiers.Select(modifier => FormatModifier(modifier, modifierCandidateResolutions)));
    }

    private static string FormatModifier(
        ParsedModifier modifier,
        ModifierCandidateResolutionsDisplay modifierCandidateResolutions)
    {
        var candidateDisplay = modifierCandidateResolutions.Results
            .FirstOrDefault(result => ReferenceEquals(result.ParsedModifier, modifier));
        var candidateLines = candidateDisplay is null || !candidateDisplay.ShowInRegularDisplay
            ? string.Empty
            : $"{Environment.NewLine}{FormatModifierCandidateResolution(candidateDisplay)}";

        if (modifier.RawMetadataLine is null
            && !modifier.IsCrafted
            && !modifier.IsFractured
            && !modifier.IsVeiled)
        {
            return $"{modifier.Text}{candidateLines}";
        }

        var metadataParts = new List<string>
        {
            modifier.Kind.ToString(),
        };

        if (modifier.IsCrafted)
        {
            metadataParts.Insert(0, "Crafted");
        }

        if (modifier.IsFractured)
        {
            metadataParts.Insert(0, "Fractured");
        }

        if (modifier.IsVeiled)
        {
            metadataParts.Insert(0, "Veiled");
        }

        if (modifier.Tier.HasValue)
        {
            metadataParts.Add($"T{modifier.Tier.Value}");
        }

        if (modifier.Rank.HasValue)
        {
            metadataParts.Add($"R{modifier.Rank.Value}");
        }

        var metadataText = $"[{string.Join(' ', metadataParts)}]";
        if (!string.IsNullOrWhiteSpace(modifier.Name))
        {
            metadataText = $"{metadataText} {modifier.Name}";
        }

        if (!string.IsNullOrWhiteSpace(modifier.CategoryText))
        {
            metadataText = $"{metadataText} — {modifier.CategoryText}";
        }

        return $"{metadataText}{Environment.NewLine}{IndentLines(modifier.ValueLines)}{candidateLines}";
    }

    private static string FormatModifierCandidateResolution(
        ModifierCandidateResolutionItemDisplay candidateDisplay)
    {
        var lines = new List<string>
        {
            $"  Parsed name: {DisplayValue(candidateDisplay.ParsedModifier.Name)}",
            $"  Candidate status: {candidateDisplay.Status}",
            $"  Candidate counts: {candidateDisplay.CountSummary}",
            $"  Candidate diagnostic: {candidateDisplay.Diagnostic}",
        };

        if (candidateDisplay.CandidateLabels.Count > 0)
        {
            lines.Add("  Candidate IDs/names:");
            lines.AddRange(candidateDisplay.CandidateLabels.Select(candidate => $"    {candidate}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string IndentLines(IReadOnlyCollection<string> lines)
    {
        return lines.Count == 0
            ? NotDetectedText
            : string.Join(Environment.NewLine, lines.Select(line => $"  {line}"));
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

    private enum ClipboardCaptureWaitResult
    {
        ClipboardChanged,
        TimedOut,
        ForegroundLost,
    }
}

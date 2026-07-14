using System.IO;
using System.Text.Json;
using Serilog;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerPlacementStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string filePath;

    public PriceCheckerPlacementStore(string filePath)
    {
        this.filePath = filePath;
    }

    public string FilePath => filePath;

    public double LoadHorizontalCorrection(PriceCheckerPlacementKey key)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return 0d;
            }

            var state = LoadState();
            return state.HorizontalCorrections.TryGetValue(key.ToStorageKey(), out var correction)
                ? correction
                : 0d;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Price Checker placement could not be loaded");
            return 0d;
        }
    }

    public double? LoadPanelWidth(PriceCheckerPlacementKey key)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var state = LoadState();
            return state.PanelWidths.TryGetValue(key.ToStorageKey(), out var width) &&
                    double.IsFinite(width) &&
                    width > 0d
                ? width
                : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Price Checker placement width could not be loaded");
            return null;
        }
    }

    public void SaveHorizontalCorrection(PriceCheckerPlacementKey key, double correction)
    {
        try
        {
            var state = File.Exists(filePath)
                ? LoadState()
                : new PriceCheckerPlacementFile();
            state.HorizontalCorrections[key.ToStorageKey()] = correction;
            SaveStateAtomically(state);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Price Checker placement could not be saved");
        }
    }

    public void SavePanelWidth(PriceCheckerPlacementKey key, double panelWidth)
    {
        if (!double.IsFinite(panelWidth) || panelWidth <= 0d)
        {
            return;
        }

        try
        {
            var state = File.Exists(filePath)
                ? LoadState()
                : new PriceCheckerPlacementFile();
            state.PanelWidths[key.ToStorageKey()] = panelWidth;
            SaveStateAtomically(state);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Price Checker placement width could not be saved");
        }
    }

    public void ResetHorizontalCorrection(PriceCheckerPlacementKey key)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var state = LoadState();
            if (state.HorizontalCorrections.Remove(key.ToStorageKey()))
            {
                SaveStateAtomically(state);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Price Checker placement could not be reset");
        }
    }

    private PriceCheckerPlacementFile LoadState()
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<PriceCheckerPlacementFile>(json, JsonOptions)
            ?? new PriceCheckerPlacementFile();
    }

    private void SaveStateAtomically(PriceCheckerPlacementFile state)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));

        if (File.Exists(filePath))
        {
            File.Replace(temporaryPath, filePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporaryPath, filePath);
        }
    }

    private sealed class PriceCheckerPlacementFile
    {
        public Dictionary<string, double> HorizontalCorrections { get; init; } = [];

        public Dictionary<string, double> PanelWidths { get; init; } = [];
    }
}

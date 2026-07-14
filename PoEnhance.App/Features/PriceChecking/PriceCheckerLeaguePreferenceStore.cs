using System.IO;
using System.Text.Json;
using Serilog;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerLeaguePreferenceStore : IPriceCheckerLeaguePreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string filePath;

    public PriceCheckerLeaguePreferenceStore(string filePath)
    {
        this.filePath = filePath;
    }

    public string FilePath => filePath;

    public string? LoadLeagueIdentifier()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<PriceCheckerLeaguePreferenceFile>(json, JsonOptions)
                ?.LeagueIdentifier;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Price Checker league preference could not be loaded");
            return null;
        }
    }

    public void SaveLeagueIdentifier(string leagueIdentifier)
    {
        try
        {
            var trimmedLeagueIdentifier = leagueIdentifier.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLeagueIdentifier))
            {
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new PriceCheckerLeaguePreferenceFile
            {
                LeagueIdentifier = trimmedLeagueIdentifier,
            };
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
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Price Checker league preference could not be saved");
        }
    }

    private sealed class PriceCheckerLeaguePreferenceFile
    {
        public string? LeagueIdentifier { get; init; }
    }
}

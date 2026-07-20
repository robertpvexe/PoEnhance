using System.IO;
using System.Text.Json;
using Serilog;

namespace PoEnhance.App.Infrastructure.Settings;

internal sealed class ApplicationLeagueSetting
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string? filePath;

    public ApplicationLeagueSetting(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
        EffectiveLeague = LoadEffectiveLeague(filePath);
    }

    private ApplicationLeagueSetting()
    {
    }

    public event EventHandler<string>? Changed;

    public string? EffectiveLeague { get; private set; }

    public string? FilePath => filePath;

    public static ApplicationLeagueSetting CreateDefault()
    {
        return new ApplicationLeagueSetting(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoEnhance",
            "settings.json"));
    }

    internal static ApplicationLeagueSetting CreateTransient(string? effectiveLeague = null)
    {
        return new ApplicationLeagueSetting
        {
            EffectiveLeague = TrimToNull(effectiveLeague),
        };
    }

    public bool TrySave(string? effectiveLeague)
    {
        var trimmedLeague = TrimToNull(effectiveLeague);
        if (trimmedLeague is null)
        {
            return false;
        }

        if (filePath is not null && !TryWrite(filePath, trimmedLeague))
        {
            return false;
        }

        if (string.Equals(EffectiveLeague, trimmedLeague, StringComparison.Ordinal))
        {
            return true;
        }

        EffectiveLeague = trimmedLeague;
        Changed?.Invoke(this, trimmedLeague);
        return true;
    }

    private static string? LoadEffectiveLeague(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<ApplicationSettingsFile>(json, JsonOptions);
            return TrimToNull(settings?.League);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Application settings could not be loaded from {FilePath}", filePath);
            return null;
        }
    }

    private static bool TryWrite(string filePath, string effectiveLeague)
    {
        string? temporaryPath = null;
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(
                    new ApplicationSettingsFile { League = effectiveLeague },
                    JsonOptions));

            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Application settings could not be saved to {FilePath}", filePath);
            return false;
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Log.Debug(exception, "Temporary application settings file could not be removed");
                }
            }
        }
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed class ApplicationSettingsFile
    {
        public string? League { get; init; }
    }
}

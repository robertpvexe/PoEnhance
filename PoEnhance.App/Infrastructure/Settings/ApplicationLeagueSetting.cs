using System.IO;
using System.Text.Json;
using PoEnhance.App.Infrastructure.Shortcuts;
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
        var settings = Load(filePath);
        EffectiveLeague = settings.League;
        QuickUseCommands = settings.QuickUseCommands;
        Log.Information(
            "Application settings loaded. LeagueConfigured={LeagueConfigured}; QuickUseRows={QuickUseRows}; QuickUseBindings={QuickUseBindings}",
            EffectiveLeague is not null,
            QuickUseCommands.Count,
            QuickUseCommands.Count(command => command.Hotkey is not null && !string.IsNullOrWhiteSpace(command.Command)));
    }

    private ApplicationLeagueSetting()
    {
    }

    public event EventHandler<string>? Changed;

    public event EventHandler? QuickUseCommandsChanged;

    public string? EffectiveLeague { get; private set; }

    public IReadOnlyList<QuickUseCommandSetting> QuickUseCommands { get; private set; }
        = QuickUseCommandSetting.CreateDefaults();

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
            QuickUseCommands = QuickUseCommandSetting.CreateDefaults(),
        };
    }

    public bool TrySave(string? effectiveLeague)
    {
        var trimmedLeague = TrimToNull(effectiveLeague);
        if (trimmedLeague is null)
        {
            return false;
        }

        if (filePath is not null && !TryWrite(filePath, trimmedLeague, QuickUseCommands))
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

    public bool TrySaveQuickUseCommands(
        IReadOnlyList<QuickUseCommandSetting> commands,
        out string validationError)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var normalized = commands
            .Select(command => command with { Command = command.Command.Trim() })
            .ToArray();

        if (!QuickUseSettingsValidator.TryValidate(normalized, out validationError))
        {
            return false;
        }

        if (filePath is not null && !TryWrite(filePath, EffectiveLeague, normalized))
        {
            validationError = "Quick Use settings could not be saved. Try again.";
            return false;
        }

        QuickUseCommands = normalized;
        Log.Information(
            "Quick Use settings saved. Rows={QuickUseRows}; ActiveBindings={QuickUseBindings}",
            QuickUseCommands.Count,
            QuickUseCommands.Count(command => command.Hotkey is not null && !string.IsNullOrWhiteSpace(command.Command)));
        QuickUseCommandsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static LoadedSettings Load(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new LoadedSettings(null, QuickUseCommandSetting.CreateDefaults());
            }

            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<ApplicationSettingsFile>(json, JsonOptions);
            var quickUseCommands = settings?.QuickUse is null
                ? QuickUseCommandSetting.CreateDefaults()
                : settings.QuickUse.Select(ToSetting).ToArray();
            if (!QuickUseSettingsValidator.TryValidate(quickUseCommands, out _))
            {
                quickUseCommands = QuickUseCommandSetting.CreateDefaults();
            }

            return new LoadedSettings(TrimToNull(settings?.League), quickUseCommands);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Application settings could not be loaded from {FilePath}", filePath);
            return new LoadedSettings(null, QuickUseCommandSetting.CreateDefaults());
        }
    }

    private static bool TryWrite(
        string filePath,
        string? effectiveLeague,
        IReadOnlyList<QuickUseCommandSetting> quickUseCommands)
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
                    new ApplicationSettingsFile
                    {
                        League = effectiveLeague,
                        QuickUse = quickUseCommands.Select(ToFile).ToArray(),
                    },
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

        public QuickUseCommandFile[]? QuickUse { get; init; }
    }

    private sealed class QuickUseCommandFile
    {
        public string? Command { get; init; }

        public bool PressEnter { get; init; }

        public ShortcutKey? Key { get; init; }

        public ShortcutModifiers Modifiers { get; init; }

        public bool IsCustom { get; init; }
    }

    private static QuickUseCommandSetting ToSetting(QuickUseCommandFile command)
    {
        return new QuickUseCommandSetting(
            command.Command ?? string.Empty,
            command.PressEnter,
            command.Key is null ? null : new ShortcutBinding(command.Key.Value, command.Modifiers),
            command.IsCustom);
    }

    private static QuickUseCommandFile ToFile(QuickUseCommandSetting command)
    {
        return new QuickUseCommandFile
        {
            Command = command.Command,
            PressEnter = command.PressEnter,
            Key = command.Hotkey?.PrimaryKey,
            Modifiers = command.Hotkey?.Modifiers ?? ShortcutModifiers.None,
            IsCustom = command.IsCustom,
        };
    }

    private sealed record LoadedSettings(
        string? League,
        IReadOnlyList<QuickUseCommandSetting> QuickUseCommands);
}

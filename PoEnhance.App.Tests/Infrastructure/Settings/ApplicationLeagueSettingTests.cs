using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Tests.Infrastructure.Settings;

public sealed class ApplicationLeagueSettingTests
{
    [Fact]
    public void MissingSettingsFile_DoesNotDefaultLeague()
    {
        using var directory = new TemporaryDirectory();
        var setting = new ApplicationLeagueSetting(Path.Combine(directory.Path, "settings.json"));

        Assert.Null(setting.EffectiveLeague);
    }

    [Fact]
    public void Save_TrimsAndPersistsEffectiveLeague()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var setting = new ApplicationLeagueSetting(path);

        Assert.True(setting.TrySave("  Settlers  "));

        Assert.Equal("Settlers", setting.EffectiveLeague);
        Assert.Equal("Settlers", new ApplicationLeagueSetting(path).EffectiveLeague);
    }

    [Fact]
    public void EmptyLeague_IsRejectedWithoutCreatingSettingsFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var setting = new ApplicationLeagueSetting(path);

        Assert.False(setting.TrySave("   "));

        Assert.Null(setting.EffectiveLeague);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DefaultPath_IsUnderPoEnhanceLocalApplicationData()
    {
        var setting = ApplicationLeagueSetting.CreateDefault();
        var expectedDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoEnhance");

        Assert.Equal(
            Path.Combine(expectedDirectory, "settings.json"),
            setting.FilePath);
    }

    [Fact]
    public void MissingQuickUseConfiguration_UsesThreePresetsAndThreeEmptyCustomRows()
    {
        using var directory = new TemporaryDirectory();
        var setting = new ApplicationLeagueSetting(Path.Combine(directory.Path, "settings.json"));

        Assert.Collection(
            setting.QuickUseCommands,
            command => AssertQuickUse(command, "/hideout", ShortcutKey.F5, isCustom: false),
            command => AssertQuickUse(command, "/kingsmarch", ShortcutKey.F6, isCustom: false),
            command => AssertQuickUse(command, "/monastery of the keepers", ShortcutKey.F7, isCustom: false),
            command => AssertEmptyCustom(command),
            command => AssertEmptyCustom(command),
            command => AssertEmptyCustom(command));
    }

    [Fact]
    public void QuickUseSave_PersistsRowsAndPreservesLeague()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var setting = new ApplicationLeagueSetting(path);
        Assert.True(setting.TrySave("Hardcore"));
        QuickUseCommandSetting[] commands =
        [
            new("/hideout", false, new ShortcutBinding(
                ShortcutKey.F10,
                ShortcutModifiers.Control), false),
            new("/custom", true, null, true),
        ];

        Assert.True(setting.TrySaveQuickUseCommands(commands, out var error), error);

        var restored = new ApplicationLeagueSetting(path);
        Assert.Equal("Hardcore", restored.EffectiveLeague);
        Assert.Equal(commands, restored.QuickUseCommands);
    }

    [Fact]
    public void LegacyLeagueOnlyFile_LoadsLeagueAndMigratesOnQuickUseSave()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "{\"League\":\"Standard\"}");
        var setting = new ApplicationLeagueSetting(path);

        Assert.Equal("Standard", setting.EffectiveLeague);
        Assert.Equal(6, setting.QuickUseCommands.Count);
        Assert.True(setting.TrySaveQuickUseCommands(setting.QuickUseCommands, out var error), error);

        var migratedJson = File.ReadAllText(path);
        Assert.Contains("\"League\": \"Standard\"", migratedJson, StringComparison.Ordinal);
        Assert.Contains("\"QuickUse\"", migratedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickUseSave_RejectsDuplicateAndReservedBindings()
    {
        var setting = ApplicationLeagueSetting.CreateTransient("Standard");
        QuickUseCommandSetting[] duplicate =
        [
            new("/one", true, new ShortcutBinding(ShortcutKey.F5, ShortcutModifiers.None), false),
            new("/two", true, new ShortcutBinding(ShortcutKey.F5, ShortcutModifiers.None), true),
        ];
        QuickUseCommandSetting[] reserved =
        [
            new("/one", true, ShortcutBinding.DefaultPriceChecker, false),
        ];

        Assert.False(setting.TrySaveQuickUseCommands(duplicate, out var duplicateError));
        Assert.Contains("already assigned", duplicateError, StringComparison.Ordinal);
        Assert.False(setting.TrySaveQuickUseCommands(reserved, out var reservedError));
        Assert.Contains("reserved", reservedError, StringComparison.Ordinal);
        Assert.Equal(6, setting.QuickUseCommands.Count);
    }

    private static void AssertQuickUse(
        QuickUseCommandSetting setting,
        string command,
        ShortcutKey key,
        bool isCustom)
    {
        Assert.Equal(command, setting.Command);
        Assert.True(setting.PressEnter);
        Assert.Equal(new ShortcutBinding(key, ShortcutModifiers.None), setting.Hotkey);
        Assert.Equal(isCustom, setting.IsCustom);
    }

    private static void AssertEmptyCustom(QuickUseCommandSetting setting)
    {
        Assert.Equal(string.Empty, setting.Command);
        Assert.True(setting.PressEnter);
        Assert.Null(setting.Hotkey);
        Assert.True(setting.IsCustom);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"PoEnhance.ApplicationLeagueSettingTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

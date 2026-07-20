using PoEnhance.App.Infrastructure.Settings;

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

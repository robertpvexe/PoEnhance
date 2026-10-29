using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Tests.Infrastructure.Settings;

public sealed class PriceCheckerMiscellaneousFilterPreferenceTests
{
    [Fact]
    public void Defaults_IncludeCurrentMiscellaneousGroupsVisibleInDisplayOrder()
    {
        var defaults = PriceCheckerMiscellaneousFilterRegistry.CreateDefaults();

        Assert.Equal(
            [
                PriceCheckerMiscellaneousFilterRegistry.FracturedItem,
                PriceCheckerMiscellaneousFilterRegistry.ItemLevel,
                PriceCheckerMiscellaneousFilterRegistry.Quality,
                PriceCheckerMiscellaneousFilterRegistry.Sockets,
                PriceCheckerMiscellaneousFilterRegistry.Links,
                PriceCheckerMiscellaneousFilterRegistry.BasePercentile,
            ],
            defaults.Select(preference => preference.Key));
        Assert.All(defaults, preference => Assert.True(preference.IsVisible));
        Assert.Equal(
            Enumerable.Range(0, defaults.Count),
            defaults.Select(preference => preference.Order));
    }

    [Fact]
    public void MissingSettingsFile_UsesDefaultOnMiscellaneousPreferences()
    {
        using var directory = new TemporaryDirectory();
        var setting = new ApplicationLeagueSetting(Path.Combine(directory.Path, "settings.json"));

        Assert.Equal(
            PriceCheckerMiscellaneousFilterRegistry.CreateDefaults(),
            setting.PriceCheckerMiscellaneousFilters);
    }

    [Fact]
    public void VisibilityAndOrder_PersistAcrossReloadWithoutChangingOtherSettings()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var setting = new ApplicationLeagueSetting(path);
        Assert.True(setting.TrySaveResolved("provider-current", "Current League"));
        Assert.True(setting.TrySaveQuickUseCommands(
            [
                new("/hideout", true, new ShortcutBinding(ShortcutKey.F5, ShortcutModifiers.None), false),
            ],
            out var quickUseError),
            quickUseError);

        var reordered = setting.PriceCheckerMiscellaneousFilters.ToList();
        var sockets = reordered.Single(preference =>
            preference.Key == PriceCheckerMiscellaneousFilterRegistry.Sockets);
        reordered.Remove(sockets);
        reordered.Insert(0, sockets);
        for (var index = 0; index < reordered.Count; index++)
        {
            var isVisible = reordered[index].Key != PriceCheckerMiscellaneousFilterRegistry.Quality;
            reordered[index] = reordered[index] with
            {
                IsVisible = isVisible,
                Order = index,
            };
        }

        Assert.True(setting.TrySavePriceCheckerMiscellaneousFilters(reordered));

        var restored = new ApplicationLeagueSetting(path);
        Assert.Equal("provider-current", restored.LeagueSelection?.ProviderId);
        Assert.Equal("Current League", restored.EffectiveLeague);
        Assert.Equal("/hideout", Assert.Single(restored.QuickUseCommands).Command);
        Assert.Equal(
            PriceCheckerMiscellaneousFilterRegistry.Sockets,
            restored.PriceCheckerMiscellaneousFilters[0].Key);
        Assert.False(
            restored.PriceCheckerMiscellaneousFilters
                .Single(preference => preference.Key == PriceCheckerMiscellaneousFilterRegistry.Quality)
                .IsVisible);
        Assert.All(
            restored.PriceCheckerMiscellaneousFilters.Where(preference =>
                preference.Key != PriceCheckerMiscellaneousFilterRegistry.Quality),
            preference => Assert.True(preference.IsVisible));
    }

    [Fact]
    public void UnknownObsoleteAndPartialEntries_MergeSafelyWithDefaultsOnForNewKeys()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(
            path,
            """
            {
              "League": "Standard",
              "PriceCheckerMiscellaneousFilters": [
                { "Key": "quality", "IsVisible": false, "Order": 0 },
                { "Key": "obsolete-filter", "IsVisible": false, "Order": 1 },
                { "Key": "quality", "IsVisible": true, "Order": 2 },
                { "Key": "  ", "IsVisible": false, "Order": 3 }
              ]
            }
            """);

        var setting = new ApplicationLeagueSetting(path);

        Assert.Equal("Standard", setting.EffectiveLeague);
        Assert.Equal(
            PriceCheckerMiscellaneousFilterRegistry.Quality,
            setting.PriceCheckerMiscellaneousFilters[0].Key);
        Assert.False(setting.PriceCheckerMiscellaneousFilters[0].IsVisible);
        Assert.DoesNotContain(
            setting.PriceCheckerMiscellaneousFilters,
            preference => preference.Key == "obsolete-filter");
        Assert.Equal(
            PriceCheckerMiscellaneousFilterRegistry.Definitions.Count,
            setting.PriceCheckerMiscellaneousFilters.Count);
        Assert.All(
            setting.PriceCheckerMiscellaneousFilters.Skip(1),
            preference => Assert.True(preference.IsVisible));
        Assert.Equal(
            [
                PriceCheckerMiscellaneousFilterRegistry.Quality,
                PriceCheckerMiscellaneousFilterRegistry.FracturedItem,
                PriceCheckerMiscellaneousFilterRegistry.ItemLevel,
                PriceCheckerMiscellaneousFilterRegistry.Sockets,
                PriceCheckerMiscellaneousFilterRegistry.Links,
                PriceCheckerMiscellaneousFilterRegistry.BasePercentile,
            ],
            setting.PriceCheckerMiscellaneousFilters.Select(preference => preference.Key));
    }

    [Fact]
    public void CorruptSettingsFile_FallsBackToDefaultsWithoutThrowing()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "{ not-json");

        var setting = new ApplicationLeagueSetting(path);

        Assert.Null(setting.EffectiveLeague);
        Assert.Equal(
            PriceCheckerMiscellaneousFilterRegistry.CreateDefaults(),
            setting.PriceCheckerMiscellaneousFilters);
    }

    [Fact]
    public void LeagueAndQuickUseSaves_PreserveMiscellaneousPreferences()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var setting = new ApplicationLeagueSetting(path);
        var preferences = setting.PriceCheckerMiscellaneousFilters
            .Select((preference, index) => preference with
            {
                IsVisible = preference.Key != PriceCheckerMiscellaneousFilterRegistry.Links,
                Order = index,
            })
            .ToArray();
        Assert.True(setting.TrySavePriceCheckerMiscellaneousFilters(preferences));
        Assert.True(setting.TrySave("Hardcore"));
        Assert.True(setting.TrySaveQuickUseCommands(setting.QuickUseCommands, out var error), error);

        var restored = new ApplicationLeagueSetting(path);
        Assert.Equal("Hardcore", restored.EffectiveLeague);
        Assert.False(
            restored.PriceCheckerMiscellaneousFilters
                .Single(preference => preference.Key == PriceCheckerMiscellaneousFilterRegistry.Links)
                .IsVisible);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"PoEnhance.PriceCheckerMiscFilterPreferenceTests.{Guid.NewGuid():N}");
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

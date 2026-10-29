using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;
using PoEnhance.Core.Trade;

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
                PriceCheckerMiscellaneousFilterRegistry.SynthesisedItem,
                PriceCheckerMiscellaneousFilterRegistry.Split,
                PriceCheckerMiscellaneousFilterRegistry.Crafted,
                PriceCheckerMiscellaneousFilterRegistry.Veiled,
                PriceCheckerMiscellaneousFilterRegistry.Foreseeing,
                PriceCheckerMiscellaneousFilterRegistry.CrucibleItem,
                PriceCheckerMiscellaneousFilterRegistry.Foulborn,
                PriceCheckerMiscellaneousFilterRegistry.Vestigial,
            ],
            defaults.Select(preference => preference.Key));
        Assert.All(defaults, preference => Assert.True(preference.IsVisible));
        Assert.Equal(
            Enumerable.Range(0, defaults.Count),
            defaults.Select(preference => preference.Order));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.SynthesisedItem));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.Split));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.Crafted));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.Veiled));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.Foreseeing));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.CrucibleItem));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.Foulborn));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.Vestigial));
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
            var isVisible = reordered[index].Key != PriceCheckerMiscellaneousFilterRegistry.BasePercentile;
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
        Assert.True(
            restored.PriceCheckerMiscellaneousFilters
                .Single(preference => preference.Key == PriceCheckerMiscellaneousFilterRegistry.Quality)
                .IsVisible);
        Assert.False(
            restored.PriceCheckerMiscellaneousFilters
                .Single(preference => preference.Key == PriceCheckerMiscellaneousFilterRegistry.BasePercentile)
                .IsVisible);
        Assert.All(
            restored.PriceCheckerMiscellaneousFilters.Where(preference =>
                preference.Key != PriceCheckerMiscellaneousFilterRegistry.BasePercentile),
            preference => Assert.True(preference.IsVisible));
    }

    [Fact]
    public void UnknownObsoleteAndPartialEntries_MergeSafelyAndNormalizePermanentVisibility()
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
                { "Key": "fractured-item", "IsVisible": false, "Order": 3 },
                { "Key": "  ", "IsVisible": false, "Order": 4 }
              ]
            }
            """);

        var setting = new ApplicationLeagueSetting(path);

        Assert.Equal("Standard", setting.EffectiveLeague);
        Assert.Equal(
            PriceCheckerMiscellaneousFilterRegistry.Quality,
            setting.PriceCheckerMiscellaneousFilters[0].Key);
        Assert.True(setting.PriceCheckerMiscellaneousFilters[0].IsVisible);
        Assert.False(
            setting.PriceCheckerMiscellaneousFilters
                .Single(preference =>
                    preference.Key == PriceCheckerMiscellaneousFilterRegistry.FracturedItem)
                .IsVisible);
        Assert.DoesNotContain(
            setting.PriceCheckerMiscellaneousFilters,
            preference => preference.Key == "obsolete-filter");
        Assert.Equal(
            PriceCheckerMiscellaneousFilterRegistry.Definitions.Count,
            setting.PriceCheckerMiscellaneousFilters.Count);
        Assert.Equal(
            [
                PriceCheckerMiscellaneousFilterRegistry.Quality,
                PriceCheckerMiscellaneousFilterRegistry.FracturedItem,
                PriceCheckerMiscellaneousFilterRegistry.ItemLevel,
                PriceCheckerMiscellaneousFilterRegistry.Sockets,
                PriceCheckerMiscellaneousFilterRegistry.Links,
                PriceCheckerMiscellaneousFilterRegistry.BasePercentile,
                PriceCheckerMiscellaneousFilterRegistry.SynthesisedItem,
                PriceCheckerMiscellaneousFilterRegistry.Split,
                PriceCheckerMiscellaneousFilterRegistry.Crafted,
                PriceCheckerMiscellaneousFilterRegistry.Veiled,
                PriceCheckerMiscellaneousFilterRegistry.Foreseeing,
                PriceCheckerMiscellaneousFilterRegistry.CrucibleItem,
                PriceCheckerMiscellaneousFilterRegistry.Foulborn,
                PriceCheckerMiscellaneousFilterRegistry.Vestigial,
            ],
            setting.PriceCheckerMiscellaneousFilters.Select(preference => preference.Key));
        Assert.All(
            setting.PriceCheckerMiscellaneousFilters.Where(preference =>
                preference.Key is
                    PriceCheckerMiscellaneousFilterRegistry.SynthesisedItem or
                    PriceCheckerMiscellaneousFilterRegistry.Split or
                    PriceCheckerMiscellaneousFilterRegistry.Crafted or
                    PriceCheckerMiscellaneousFilterRegistry.Veiled or
                    PriceCheckerMiscellaneousFilterRegistry.Foreseeing or
                    PriceCheckerMiscellaneousFilterRegistry.CrucibleItem or
                    PriceCheckerMiscellaneousFilterRegistry.Foulborn or
                    PriceCheckerMiscellaneousFilterRegistry.Vestigial),
            preference => Assert.True(preference.IsVisible));
        Assert.False(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.Quality));
        Assert.True(PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(
            PriceCheckerMiscellaneousFilterRegistry.FracturedItem));
    }

    [Fact]
    public void ForSearch_ExcludesHiddenOptionalConstraintsWithoutMutatingPermanentGroups()
    {
        var draft = new TradeSearchDraft
        {
            ItemClass = "Rings",
            Rarity = "Rare",
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Fractured = TradeTriState.Yes,
                Corrupted = TradeTriState.No,
                Synthesised = TradeTriState.Yes,
                Split = TradeTriState.No,
                Crafted = TradeTriState.Yes,
                Veiled = TradeTriState.No,
                Foreseeing = TradeTriState.Yes,
                Crucible = TradeTriState.No,
                Foulborn = TradeTriState.Yes,
                Vestigial = TradeTriState.No,
            },
            RequestedItemFilters =
            [
                new TradeSearchRequestedItemFilter
                {
                    Kind = TradeSearchRequestedItemFilterKind.Quality,
                    Label = "Quality",
                    CurrentText = "20",
                    RequestedMinimum = 20,
                    IsActive = true,
                    LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
                },
                new TradeSearchRequestedItemFilter
                {
                    Kind = TradeSearchRequestedItemFilterKind.BasePercentile,
                    Label = "Base Percentile",
                    CurrentText = "80",
                    RequestedMinimum = 80,
                    IsActive = true,
                    LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
                },
            ],
        };
        var preferences = PriceCheckerMiscellaneousFilterRegistry.CreateDefaults()
            .Select(preference => preference.Key switch
            {
                PriceCheckerMiscellaneousFilterRegistry.FracturedItem =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.BasePercentile =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.SynthesisedItem =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.Split =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.Crafted =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.Veiled =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.Foreseeing =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.CrucibleItem =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.Foulborn =>
                    preference with { IsVisible = false },
                PriceCheckerMiscellaneousFilterRegistry.Vestigial =>
                    preference with { IsVisible = false },
                _ => preference,
            })
            .ToArray();

        var searchDraft = PriceCheckerMiscellaneousFilterPreferenceApplicator.ForSearch(
            draft,
            preferences);

        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Fractured);
        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Synthesised);
        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Split);
        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Crafted);
        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Veiled);
        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Foreseeing);
        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Crucible);
        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.Any, searchDraft.ItemStateCriteria.Vestigial);
        Assert.Equal(TradeTriState.No, searchDraft.ItemStateCriteria.Corrupted);
        Assert.True(
            searchDraft.RequestedItemFilters
                .Single(filter => filter.Kind == TradeSearchRequestedItemFilterKind.Quality)
                .IsActive);
        Assert.False(
            searchDraft.RequestedItemFilters
                .Single(filter => filter.Kind == TradeSearchRequestedItemFilterKind.BasePercentile)
                .IsActive);

        var restored = PriceCheckerMiscellaneousFilterPreferenceApplicator.RestoreHiddenOptionalUiState(
            searchDraft,
            draft,
            preferences);
        Assert.Equal(TradeTriState.Yes, restored.ItemStateCriteria.Fractured);
        Assert.Equal(TradeTriState.Yes, restored.ItemStateCriteria.Synthesised);
        Assert.Equal(TradeTriState.No, restored.ItemStateCriteria.Split);
        Assert.Equal(TradeTriState.Yes, restored.ItemStateCriteria.Crafted);
        Assert.Equal(TradeTriState.No, restored.ItemStateCriteria.Veiled);
        Assert.Equal(TradeTriState.Yes, restored.ItemStateCriteria.Foreseeing);
        Assert.Equal(TradeTriState.No, restored.ItemStateCriteria.Crucible);
        Assert.Equal(TradeTriState.Yes, restored.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.No, restored.ItemStateCriteria.Vestigial);
        Assert.True(
            restored.RequestedItemFilters
                .Single(filter => filter.Kind == TradeSearchRequestedItemFilterKind.BasePercentile)
                .IsActive);
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
                IsVisible = preference.Key != PriceCheckerMiscellaneousFilterRegistry.FracturedItem,
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
                .Single(preference => preference.Key == PriceCheckerMiscellaneousFilterRegistry.FracturedItem)
                .IsVisible);
        Assert.True(
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

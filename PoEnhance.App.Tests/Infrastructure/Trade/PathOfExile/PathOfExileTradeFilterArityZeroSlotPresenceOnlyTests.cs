using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Diagnostics.Replay;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// A.5.44 — FilterArity zero-slot presence-only: real Mjölner Lightning Trigger regression
/// plus official-catalog structural controls. No item/ModId/provider-id production hardcodes.
/// </summary>
public sealed class PathOfExileTradeFilterArityZeroSlotPresenceOnlyTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    [Fact]
    public async Task RealMjolnerLightningTrigger_ZeroAritySuppressesBounds_PresenceOnlyQuery()
    {
        var raw = await ReadMjolnerCaptureClipboardAsync();
        var execution = ModifierPipelineReplayProductionPath.Execute(
            raw,
            GameData.Value,
            OfficialTradeCatalog.Value,
            TradeItemCatalog,
            FilterCatalog);

        var trigger = Assert.Single(
            execution.ProviderDraft.ModifierFilters,
            filter => filter.OriginalText.Contains(
                "Socketed Lightning Spell on Hit",
                StringComparison.Ordinal));

        // Core/draft may still remember the parsed literal; provider Exact must not emit it.
        Assert.Contains(0.25m, trigger.ObservedNumericValues);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, trigger.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_654971543", trigger.ProviderStatId);
        Assert.False(trigger.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.PresenceOnly, trigger.ValueBoundShape);
        Assert.Null(trigger.RequestedMinimum);
        Assert.Null(trigger.RequestedMaximum);
        Assert.Contains("FilterArity is 0", trigger.ValueBoundsUnsupportedReason);

        Assert.True(OfficialTradeCatalog.Value.TryGetById(trigger.ProviderStatId!, out var entry));
        Assert.Equal(
            0,
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(entry.Text));

        var selectedDraft = execution.ProviderDraft with
        {
            ModifierFilters = execution.ProviderDraft.ModifierFilters
                .Select(candidate => candidate with
                {
                    IsSelected = string.Equals(
                        candidate.ComponentId,
                        trigger.ComponentId,
                        StringComparison.Ordinal),
                })
                .ToArray(),
        };
        var mapping = SelectedMapper.Map(selectedDraft, OfficialTradeCatalog.Value);
        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(d => d.Message)));
        var filter = Assert.Single(mapping.Filters);
        Assert.Equal("explicit.stat_654971543", filter.StatId);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);

        var propertyMapping = new PathOfExileTradeItemPropertyResolver()
            .MapSelected(selectedDraft, FilterCatalog);
        Assert.True(propertyMapping.IsSuccess);
        var query = new PathOfExileTradeQueryBuilder().Build(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            "Allflame",
            mapping.Filters,
            execution.UniqueResolution is null
                ? null
                : new PathOfExileTradeItemIdentityMapper()
                    .Map(selectedDraft, TradeItemCatalog)
                    .Identity,
            FilterCatalog,
            propertyMapping.Filters);
        Assert.True(query.IsSuccess, string.Join(" | ", query.Diagnostics.Select(d => d.Message)));
        var serializedJson = Assert.IsType<string>(query.SerializedJson);
        Assert.Contains("explicit.stat_654971543", serializedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"min\":0.25", serializedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"max\":0.25", serializedJson, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(serializedJson);
        var triggerFilter = document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray()
            .SelectMany(group => group.GetProperty("filters").EnumerateArray())
            .Single(element =>
                element.GetProperty("id").GetString() == "explicit.stat_654971543");
        Assert.False(triggerFilter.TryGetProperty("value", out _));
    }

    [Fact]
    public void OfficialCatalog_ZeroArityLiteralCraftedModifiers_OmitsBounds()
    {
        var entry = OfficialTradeCatalog.Value.Entries.First(candidate =>
            candidate.Id == "explicit.stat_1859333175");
        Assert.Equal(0, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(entry.Text));
        Assert.Contains("3", entry.Text, StringComparison.Ordinal);

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                ValueBoundShape = ModifierBoundShape.Scalar,
                SupportsValueBounds = true,
                ObservedNumericValues = [3m],
                CanonicalNumericValues = [3m],
                RequestedMinimum = 3m,
                RequestedMaximum = 3m,
                ProviderStatId = entry.Id,
            },
            PathOfExileTradeStatCandidateClassifier.ToCandidate(entry));

        Assert.Null(bounds.Minimum);
        Assert.Null(bounds.Maximum);
        Assert.Equal(ModifierBoundShape.PresenceOnly, bounds.ValueBoundShape);
    }

    [Theory]
    [InlineData("explicit.stat_876831634", "Cannot be Frozen")]
    [InlineData("explicit.stat_283649372", "Cannot be Chilled")]
    public void OfficialCatalog_ZeroArityPresenceOnly_OmitsBounds(string statId, string expectedText)
    {
        Assert.True(OfficialTradeCatalog.Value.TryGetById(statId, out var entry));
        Assert.Equal(expectedText, entry.Text);
        Assert.Equal(0, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(entry.Text));

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                SupportsValueBounds = false,
                ProviderStatId = entry.Id,
            },
            PathOfExileTradeStatCandidateClassifier.ToCandidate(entry));

        Assert.Null(bounds.Minimum);
        Assert.Null(bounds.Maximum);
    }

    [Theory]
    [InlineData("explicit.stat_3299347043", "+# to maximum Life", 100)]
    [InlineData("explicit.stat_4220027924", "+#% to Cold Resistance", 40)]
    [InlineData("explicit.stat_1050105434", "+# to maximum Mana", 55)]
    public void OfficialCatalog_OneArityNumeric_PreservesBounds(
        string statId,
        string expectedText,
        int value)
    {
        Assert.True(OfficialTradeCatalog.Value.TryGetById(statId, out var entry));
        Assert.Equal(expectedText, entry.Text);
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(entry.Text));

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                ValueBoundShape = ModifierBoundShape.Scalar,
                SupportsValueBounds = true,
                ObservedNumericValues = [value],
                CanonicalNumericValues = [value],
                RequestedMinimum = value,
                ProviderStatId = entry.Id,
            },
            PathOfExileTradeStatCandidateClassifier.ToCandidate(entry));

        Assert.Equal(value, bounds.Minimum);
        Assert.Null(bounds.Maximum);
    }

    [Theory]
    [InlineData(
        "explicit.stat_2918708827",
        "#% chance to gain Phasing for 4 seconds on Kill",
        20)]
    [InlineData(
        "explicit.stat_3044826007",
        "Monsters have #% chance to inflict Withered for 2 seconds on Hit",
        15)]
    public void OfficialCatalog_MixedLiteralDynamic_ArityOne_PreservesDynamicOnly(
        string statId,
        string expectedText,
        int dynamicValue)
    {
        Assert.True(OfficialTradeCatalog.Value.TryGetById(statId, out var entry));
        Assert.Equal(expectedText, entry.Text);
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(entry.Text));

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                ValueBoundShape = ModifierBoundShape.Scalar,
                SupportsValueBounds = true,
                ObservedNumericValues = [dynamicValue],
                CanonicalNumericValues = [dynamicValue],
                RequestedMinimum = dynamicValue,
                ProviderStatId = entry.Id,
            },
            PathOfExileTradeStatCandidateClassifier.ToCandidate(entry));

        Assert.Equal(dynamicValue, bounds.Minimum);
        Assert.Null(bounds.Maximum);
    }

    [Theory]
    [InlineData("explicit.stat_3032590688", "Adds # to # Physical Damage to Attacks")]
    [InlineData("explicit.stat_1940865751", "Adds # to # Physical Damage (Local)")]
    public void OfficialCatalog_MultiSlotArityTwo_ArithmeticMeanRemainsSupported(
        string statId,
        string expectedText)
    {
        Assert.True(OfficialTradeCatalog.Value.TryGetById(statId, out var entry));
        Assert.Equal(expectedText, entry.Text);
        Assert.Equal(2, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(entry.Text));

        var projected = PathOfExileTradeModifierBoundProjector.Project(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                ValueBoundShape = ModifierBoundShape.ArithmeticMeanRange,
                ObservedNumericValues = [14m, 25m],
                SupportsValueBounds = false,
                ValueBoundsUnsupportedReason = "Provider confirmation required.",
                ProviderStatId = entry.Id,
            },
            PathOfExileTradeStatCandidateClassifier.ToCandidate(entry));

        Assert.True(projected.SupportsValueBounds);
        Assert.Equal(19.5m, projected.RequestedMinimum);
        Assert.Null(projected.RequestedMaximum);
    }

    private static PathOfExileTradeItemCatalog CreateTradeItemCatalog() =>
        new(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "weapon",
                GroupLabel = "weapon",
                Name = "Mjölner",
                Type = "Gavel",
                IsUnique = true,
            },
        ]);

    private static GameDataCatalog LoadGameData()
    {
        var result = GameDataPackageLoader
            .LoadFromFileAsync(FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("3.29.1.2.9-unique-newline-translation-fidelity", result.Package!.Manifest.DataVersion);
        return GameDataCatalog.FromPackage(result.Package);
    }

    private static PathOfExileTradeStatCatalog LoadOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var parsed = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(parsed.IsSuccess);
        return Assert.IsType<PathOfExileTradeStatCatalog>(parsed.Catalog);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativeParts)}");
    }

    private static async Task<string> ReadMjolnerCaptureClipboardAsync()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-A.5.42-ManualValidation");
        Assert.True(Directory.Exists(directory), $"Missing Mjölner capture directory: {directory}");
        var path = Directory.EnumerateFiles(directory, "*.json")
            .First(file =>
                Path.GetFileName(file).Contains("104408", StringComparison.Ordinal) &&
                !Path.GetFileName(file).Contains("search", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(file).Contains("request", StringComparison.OrdinalIgnoreCase));
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        var raw = document.RootElement
            .GetProperty("replayContext")
            .GetProperty("rawClipboardText")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));
        return raw!;
    }
}

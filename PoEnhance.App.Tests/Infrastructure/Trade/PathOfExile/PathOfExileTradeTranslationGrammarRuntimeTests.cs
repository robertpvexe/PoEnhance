using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4c/4e — real StatId + official Trade catalog regressions (Writhing / Steelworm /
/// Hungry Loop / Tempered Spirit).
/// </summary>
public sealed class PathOfExileTradeTranslationGrammarRuntimeTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_WrithingJar_OfficialCatalog_ExactPresenceOnly()
    {
        var component = ExactUnique(
            "2 Enemy Writhing Worms escape the Flask when used\nWrithing Worms are destroyed when Hit",
            "<number> Enemy Writhing Worms escape the Flask when used\nWrithing Worms are destroyed when Hit",
            "SummonsWormsOnUse",
            ["local_number_of_bloodworms_to_spawn_on_flask_use"],
            [2m]);
        var result = matcher.Match(component, OfficialTradeCatalog.Value, Context());

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_2434293916", result.ExactCandidate?.StatId);
        Assert.Equal(
            "An Enemy Writhing Worms escape the Flask when used\nWrithing Worms are destroyed when Hit",
            result.ExactCandidate!.Text);
        Assert.Equal(0, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            result.ExactCandidate.Text));

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, result.ExactCandidate);
        Assert.Equal(ModifierBoundShape.PresenceOnly, bounds.ValueBoundShape);
        Assert.Null(bounds.Minimum);
        Assert.Null(bounds.Maximum);
    }

    [Fact]
    public void Match_Steelworm_OfficialCatalog_ExactProjectileScalar()
    {
        var component = ExactUnique(
            "Skills Fire 3 additional Projectiles for 4 seconds after\nyou consume a total of 8 Steel Shards",
            "Skills Fire <number> additional Projectiles for <number> seconds after\nyou consume a total of <number> Steel Shards",
            "AdditionalProjectilesAfterAmmoConsumedUniqueBelt__1",
            ["skills_fire_x_additional_projectiles_for_4_seconds_after_consuming_8_steel_ammo"],
            [3m, 4m, 8m]);
        var result = matcher.Match(component, OfficialTradeCatalog.Value, Context());

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1031404836", result.ExactCandidate?.StatId);
        Assert.Equal(
            "Skills Fire # additional Projectile for 4 seconds after\nyou consume a total of 8 Steel Shards",
            result.ExactCandidate!.Text);

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, result.ExactCandidate);
        Assert.Equal(ModifierBoundShape.Scalar, bounds.ValueBoundShape);
        Assert.Equal(3m, bounds.Minimum);
        Assert.Equal(3m, bounds.Maximum);
        Assert.Contains("4 seconds", result.ExactCandidate.Text, StringComparison.Ordinal);
        Assert.Contains("8 Steel Shards", result.ExactCandidate.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_HungryLoop_OfficialCatalog_ExactAdditionalHelperBranch()
    {
        var component = ExactUnique(
            "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume 4 Uncorrupted Support Gems\nHas not Consumed any Gems",
            "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume <number> Uncorrupted Support Gems\nHas not Consumed any Gems",
            "ConsumesSupportGemsUnique",
            ["local_unique_hungry_loop_number_of_gems_to_consume"],
            [4m]);
        var result = matcher.Match(component, OfficialTradeCatalog.Value, Context());

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_3221550523", result.ExactCandidate?.StatId);
        Assert.Equal(
            "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume # additional Uncorrupted Support Gems",
            result.ExactCandidate!.Text);
        Assert.DoesNotContain("Has not Consumed", result.ExactCandidate.Text, StringComparison.Ordinal);
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            result.ExactCandidate.Text));

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            component,
            result.ExactCandidate);
        Assert.Equal(ModifierBoundShape.Scalar, bounds.ValueBoundShape);
        Assert.Equal(4m, bounds.Minimum);
        Assert.Equal(4m, bounds.Maximum);
    }

    [Fact]
    public void Match_TemperedSpirit_OfficialCatalog_ExactUnsignedSignedScalar()
    {
        var component = ExactUnique(
            "-1 Dexterity per 1 Dexterity on Allocated Passives in Radius",
            "-<number> Dexterity per <number> Dexterity on Allocated Passives in Radius",
            "AdditionalDexterityPerAllocatedDexterityJewelUnique__1",
            [
                "local_unique_jewel_X_dexterity_per_1_dexterity_allocated_in_radius",
                "local_jewel_effect_base_radius",
            ],
            [-1m, 1m]);
        var result = matcher.Match(component, OfficialTradeCatalog.Value, Context());

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_172076472", result.ExactCandidate?.StatId);
        Assert.Equal(
            "# Dexterity per 1 Dexterity on Allocated Passives in Radius",
            result.ExactCandidate!.Text);
        Assert.Contains(" per 1 ", result.ExactCandidate.Text, StringComparison.Ordinal);
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            result.ExactCandidate.Text));

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            component,
            result.ExactCandidate);
        Assert.Equal(ModifierBoundShape.Scalar, bounds.ValueBoundShape);
        Assert.Equal(-1m, bounds.Minimum);
        Assert.Equal(-1m, bounds.Maximum);
        Assert.Equal("SignedSourceUnsignedTradeMagnitude", bounds.ProjectionKind);
    }

    private PathOfExileTradeStatMatchContext Context() =>
        new() { GameDataCatalog = GameData.Value };

    private static ResolvedSearchComponent ExactUnique(
        string original,
        string signature,
        string modId,
        IReadOnlyList<string> statIds,
        IReadOnlyList<decimal>? observed = null) =>
        new()
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = original,
            CanonicalSignature = signature,
            ProviderCanonicalSignature = signature,
            ProviderSearchSignatures = [signature, .. original.Split('\n')],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = modId,
            ResolvedStatIds = statIds.ToArray(),
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = observed?.ToArray() ?? [],
            ProviderFallbackNumericValues = observed?.ToArray() ?? [],
            RequestedMinimum = observed is { Count: > 0 } ? observed[0] : null,
            RequestedMaximum = observed is { Count: > 0 } ? observed[0] : null,
        };

    private static GameDataCatalog LoadGameData()
    {
        var result = GameDataPackageLoader
            .LoadFromFileAsync(FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(result.Package));
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
}

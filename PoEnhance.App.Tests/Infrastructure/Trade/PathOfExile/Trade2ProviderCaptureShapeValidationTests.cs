using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.2 — structural capture-shape validation for Track A/B using official Trade catalog
/// texts (no item-name production rules). BigManual captures are stale for pipeline packaging
/// but supply the Exact-core semantic shapes exercised here.
/// </summary>
public sealed class Trade2ProviderCaptureShapeValidationTests
{
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Theory]
    [InlineData("No Physical Damage")]
    public void TrackA_NoPhysicalDamage_OfficialCatalogExactCompanionFamily(string sourceText)
    {
        var result = matcher.Match(
            ExactUnique(
                sourceText,
                sourceText,
                "local_weapon_no_physical_damage",
                providerSearchSignatures: [sourceText, "<number>% increased Physical Damage"]),
            OfficialTradeCatalog.Value);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1509134228", result.ExactCandidate?.StatId);
        Assert.Equal("#% increased Physical Damage", result.ExactCandidate?.Text);
        Assert.NotEqual("Deal no Physical Damage", result.ExactCandidate?.Text);
        Assert.Equal(
            1,
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(result.ExactCandidate!.Text));
    }

    [Theory]
    [InlineData(
        "Precision has <number>% increased Mana Reservation Efficiency",
        "explicit.stat_3859865977")]
    [InlineData(
        "Gain Unholy Might for 4 seconds on Critical Strike",
        "explicit.stat_2959020308")]
    [InlineData(
        "<number>% chance for Poisons inflicted with this Weapon to deal 300% more Damage",
        "explicit.stat_768124628")]
    [InlineData(
        "Enemies Frozen by you take 20% increased Damage",
        "explicit.stat_849085925")]
    [InlineData(
        "<number>% increased Attack Speed per 25 Dexterity",
        "explicit.stat_2241560081")]
    [InlineData(
        "<number>% increased Spell Damage per 10 Intelligence",
        "explicit.stat_2818518881")]
    [InlineData(
        "+<number> to Maximum Life per 10 Intelligence",
        "explicit.stat_1114351662")]
    public void TrackB_EmbeddedConstantShapes_OfficialCatalogExact(
        string signature,
        string expectedStatId)
    {
        Assert.True(OfficialTradeCatalog.Value.TryGetById(expectedStatId, out var entry));
        var result = matcher.Match(
            ExactUnique(signature.Replace("<number>", "1", StringComparison.Ordinal), signature, "stat"),
            OfficialTradeCatalog.Value);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal(expectedStatId, result.ExactCandidate?.StatId);
        Assert.Equal(entry.Text, result.ExactCandidate?.Text);
    }

    private static ResolvedSearchComponent ExactUnique(
        string original,
        string signature,
        string statId,
        IReadOnlyList<string>? providerSearchSignatures = null) =>
        new()
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = original,
            CanonicalSignature = signature,
            ProviderCanonicalSignature = signature,
            ProviderSearchSignatures = providerSearchSignatures ?? [signature],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "unique-mod:trade2",
            ResolvedStatIds = [statId],
            UniqueCatalogBlockIds = ["unique-block:trade2"],
            UniqueSourceObservationIds = ["pob-observation:trade2"],
            IsSearchable = true,
            SupportsValueBounds = signature.Contains("<number>", StringComparison.Ordinal),
            ValueBoundShape = signature.Contains("<number>", StringComparison.Ordinal)
                ? ModifierBoundShape.Scalar
                : ModifierBoundShape.PresenceOnly,
        };

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

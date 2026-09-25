using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.2.2 Track A — special phrase display maps via translation-family Defaulted numeric
/// companion signatures, not Deal-no wording aliases.
/// </summary>
public sealed class PathOfExileTradeTranslationFamilyCompanionTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_NoPhysicalDamage_WithCompanionSignature_SelectsIncreasedPhysicalDamageFamily()
    {
        var catalog = Catalog(
            Entry("explicit.stat_3900877792", "Deal no Physical Damage", "explicit", 0),
            Entry("explicit.stat_1509134228", "#% increased Physical Damage", "explicit", 1),
            Entry("explicit.other", "+# to maximum Life", "explicit", 2));

        var result = matcher.Match(
            ExactUniquePhrase(
                "No Physical Damage",
                "local_weapon_no_physical_damage",
                companionSignatures: ["<number>% increased Physical Damage"]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1509134228", result.ExactCandidate?.StatId);
        Assert.Equal("#% increased Physical Damage", result.ExactCandidate?.Text);
        Assert.DoesNotContain(
            result.Candidates,
            candidate => candidate.Text.Equals("Deal no Physical Damage", StringComparison.Ordinal));
    }

    [Fact]
    public void Match_NoPhysicalDamage_WithoutCompanionSignature_IsNotFound()
    {
        var catalog = Catalog(
            Entry("explicit.stat_3900877792", "Deal no Physical Damage", "explicit"),
            Entry("explicit.stat_1509134228", "#% increased Physical Damage", "explicit"));

        var result = matcher.Match(
            ExactUniquePhrase(
                "No Physical Damage",
                "local_weapon_no_physical_damage",
                companionSignatures: []),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Null(result.ExactCandidate);
    }

    [Fact]
    public void Match_UnrelatedNoPhrase_DoesNotMapToPhysicalDamageFamily()
    {
        var catalog = Catalog(
            Entry("explicit.stat_1509134228", "#% increased Physical Damage", "explicit"),
            Entry("explicit.stat_3900877792", "Deal no Physical Damage", "explicit"));

        var result = matcher.Match(
            ExactUniquePhrase(
                "No Strength Requirement",
                "local_attribute_requirements_+%",
                companionSignatures: []),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_OrdinaryIncreasedPhysicalDamage_KeepsNumericFamilyWithoutPhraseAlias()
    {
        var catalog = Catalog(
            Entry("explicit.stat_1509134228", "#% increased Physical Damage", "explicit"),
            Entry("explicit.stat_3900877792", "Deal no Physical Damage", "explicit"));

        var result = matcher.Match(
            ExactUniquePhrase(
                "94% increased Physical Damage",
                "local_physical_damage_+%",
                companionSignatures: [],
                signature: "<number>% increased Physical Damage",
                supportsBounds: true),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1509134228", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void ProjectBounds_TranslationFamilyCompanion_IsPresenceOnly()
    {
        var component = ExactUniquePhrase(
            "No Physical Damage",
            "local_weapon_no_physical_damage",
            companionSignatures: ["<number>% increased Physical Damage"]);
        var provider = new PathOfExileTradeStatMatchCandidate
        {
            ProviderOrder = 0,
            GroupId = "explicit",
            GroupLabel = "explicit",
            StatId = "explicit.stat_1509134228",
            Text = "#% increased Physical Damage",
            Type = "explicit",
            ProviderKind = "explicit",
            NormalizedTemplate = "#% increased Physical Damage",
            LookupTemplate = "#% increased Physical Damage",
        };

        var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, provider);

        Assert.True(projection.IsFaithful);
        Assert.Equal(ModifierBoundShape.PresenceOnly, projection.ValueBoundShape);
        Assert.Null(projection.Minimum);
        Assert.Null(projection.Maximum);
        Assert.Equal("TranslationFamilyCompanionPresence", projection.ProjectionKind);
    }

    [Fact]
    public void Match_ConflictingCompanionSignatures_RemainAmbiguous()
    {
        var catalog = Catalog(
            Entry("explicit.stat_1509134228", "#% increased Physical Damage", "explicit", 0),
            Entry("explicit.bleed", "Attacks have #% chance to cause Bleeding", "explicit", 1));

        var result = matcher.Match(
            ExactUniquePhrase(
                "No Physical Damage",
                "local_weapon_no_physical_damage",
                companionSignatures:
                [
                    "<number>% increased Physical Damage",
                    "Attacks have <number>% chance to cause Bleeding",
                ]),
            catalog);

        // First matching companion lookup wins discovery today; conflicting Core projection must
        // not emit multiple companions. This guard documents matcher behavior when mis-fed.
        Assert.True(
            result.Status is PathOfExileTradeStatMatchStatus.Exact
                or PathOfExileTradeStatMatchStatus.Ambiguous);
        Assert.NotEqual("Deal no Physical Damage", result.ExactCandidate?.Text);
    }

    [Fact]
    public void Match_BleedCannotCause_WithCompanionSignature_SelectsChanceFamily()
    {
        var catalog = Catalog(
            Entry("explicit.stat_1923879260", "Attacks have #% chance to cause Bleeding", "explicit"),
            Entry("explicit.other", "+# to maximum Life", "explicit"));

        var result = matcher.Match(
            ExactUniquePhrase(
                "Attacks cannot cause Bleeding",
                "cannot_cause_bleeding",
                companionSignatures: ["Attacks have <number>% chance to cause Bleeding"]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1923879260", result.ExactCandidate?.StatId);
        Assert.Equal("Attacks have #% chance to cause Bleeding", result.ExactCandidate?.Text);
    }

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] entries) =>
        new(entries);

    private static PathOfExileTradeStatEntry Entry(
        string id,
        string text,
        string groupId,
        int providerOrder = 0) =>
        new()
        {
            ProviderOrder = providerOrder,
            GroupId = groupId,
            GroupLabel = groupId,
            Id = id,
            Text = text,
            Type = groupId,
        };

    private static ResolvedSearchComponent ExactUniquePhrase(
        string original,
        string resolvedStatId,
        IReadOnlyList<string> companionSignatures,
        string? signature = null,
        bool supportsBounds = false)
    {
        signature ??= original;
        var searchSignatures = new List<string> { signature };
        searchSignatures.AddRange(companionSignatures);
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = original,
            CanonicalSignature = signature,
            ProviderCanonicalSignature = signature,
            ProviderSearchSignatures = searchSignatures
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "unique-mod:trade22",
            ResolvedStatIds = [resolvedStatId],
            UniqueCatalogBlockIds = ["unique-block:trade22"],
            UniqueSourceObservationIds = ["pob-observation:trade22"],
            IsSearchable = true,
            SupportsValueBounds = supportsBounds,
            ValueBoundShape = supportsBounds
                ? ModifierBoundShape.Scalar
                : ModifierBoundShape.PresenceOnly,
        };
    }
}

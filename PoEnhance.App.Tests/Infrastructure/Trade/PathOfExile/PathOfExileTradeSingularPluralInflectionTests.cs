using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4c Track B — Exact Unique plural translation branch discovers Trade singular noun form.
/// </summary>
public sealed class PathOfExileTradeSingularPluralInflectionTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_PluralBranch_WithSingularSibling_ResolvesExactScalar()
    {
        var catalog = Catalog(
            Entry(
                "explicit.steelworm",
                "Skills Fire # additional Projectile for 4 seconds after\nyou consume a total of 8 Steel Shards",
                "explicit"));
        var component = ExactUnique(
            "Skills Fire 3 additional Projectiles for 4 seconds after\nyou consume a total of 8 Steel Shards",
            "Skills Fire <number> additional Projectiles for <number> seconds after\nyou consume a total of <number> Steel Shards",
            ["skills_fire_x_additional_projectiles_for_4_seconds_after_consuming_8_steel_ammo"],
            [3m, 4m, 8m],
            Translation(
                "skills_fire_x_additional_projectiles_for_4_seconds_after_consuming_8_steel_ammo",
                SingularVariant(
                    "Skills Fire {0} additional Projectile for 4 seconds after",
                    "you consume a total of 8 Steel Shards"),
                PluralVariant(
                    "Skills Fire {0} additional Projectiles for 4 seconds after",
                    "you consume a total of 8 Steel Shards")));
        var result = matcher.Match(component, catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.steelworm", result.ExactCandidate?.StatId);
        Assert.Contains("Projectile for 4 seconds", result.ExactCandidate!.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Projectiles", result.ExactCandidate.Text, StringComparison.Ordinal);
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            result.ExactCandidate.Text));

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, result.ExactCandidate);
        Assert.Equal(ModifierBoundShape.Scalar, bounds.ValueBoundShape);
        Assert.Equal(3m, bounds.Minimum);
        Assert.Equal(3m, bounds.Maximum);
        Assert.Equal("SingularPluralInflectionScalar", bounds.ProjectionKind);
        Assert.Contains("4 seconds", result.ExactCandidate.Text, StringComparison.Ordinal);
        Assert.Contains("8 Steel Shards", result.ExactCandidate.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_SyntheticPluralSibling_ResolvesExact()
    {
        var catalog = Catalog(
            Entry("explicit.synthetic", "Skills Fire # additional Bolt for 2 seconds", "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "Skills Fire 5 additional Bolts for 2 seconds",
                "Skills Fire <number> additional Bolts for <number> seconds",
                ["synthetic_inflection_stat"],
                [5m, 2m],
                Translation(
                    "synthetic_inflection_stat",
                    SingularVariant("Skills Fire {0} additional Bolt for 2 seconds"),
                    PluralVariant("Skills Fire {0} additional Bolts for 2 seconds"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.synthetic", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_WithoutSingularSibling_RemainsNotFound()
    {
        var catalog = Catalog(
            Entry("explicit.singular", "Skills Fire # additional Bolt for 2 seconds", "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "Skills Fire 5 additional Bolts for 2 seconds",
                "Skills Fire <number> additional Bolts for <number> seconds",
                ["synthetic_plural_only_stat"],
                [5m, 2m],
                Translation(
                    "synthetic_plural_only_stat",
                    PluralVariant("Skills Fire {0} additional Bolts for 2 seconds"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_UnrelatedFamily_Rejected()
    {
        var catalog = Catalog(
            Entry(
                "explicit.steelworm",
                "Skills Fire # additional Projectile for 4 seconds after\nyou consume a total of 8 Steel Shards",
                "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "Skills Fire 3 additional Projectiles for 4 seconds after\nyou consume a total of 8 Steel Shards",
                "Skills Fire <number> additional Projectiles for <number> seconds after\nyou consume a total of <number> Steel Shards",
                ["other_inflection_stat"],
                [3m, 4m, 8m],
                Translation(
                    "other_inflection_stat",
                    SingularVariant("Totems fire {0} additional Projectile"),
                    PluralVariant("Totems fire {0} additional Projectiles"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_IrregularUnprovenPlural_FailClosed()
    {
        var catalog = Catalog(Entry("explicit.child", "Summon # child", "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "Summon 3 children",
                "Summon <number> children",
                ["irregular_stat"],
                [3m],
                Translation(
                    "irregular_stat",
                    new StatTranslationVariant
                    {
                        Conditions = [new StatTranslationCondition { Index = 0, MinValue = 1, MaxValue = 1 }],
                        ValueFormats = ["#"],
                        FormatLines = ["Summon {0} child"],
                    },
                    new StatTranslationVariant
                    {
                        Conditions = [new StatTranslationCondition { Index = 0, MinValue = 2, MaxValue = null }],
                        ValueFormats = ["#"],
                        FormatLines = ["Summon {0} children"],
                    })),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_MultipleSingularCandidates_RemainAmbiguous()
    {
        var catalog = Catalog(
            Entry("explicit.a", "Skills Fire # additional Bolt for 2 seconds", "explicit", 0),
            Entry("explicit.b", "Skills Fire # additional Bolt for 2 seconds", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                "Skills Fire 5 additional Bolts for 2 seconds",
                "Skills Fire <number> additional Bolts for <number> seconds",
                ["synthetic_inflection_stat"],
                [5m, 2m],
                Translation(
                    "synthetic_inflection_stat",
                    SingularVariant("Skills Fire {0} additional Bolt for 2 seconds"),
                    PluralVariant("Skills Fire {0} additional Bolts for 2 seconds"))),
            catalog);

        Assert.True(
            result.Status is PathOfExileTradeStatMatchStatus.Ambiguous or
                PathOfExileTradeStatMatchStatus.ExactEquivalentSet);
    }

    [Fact]
    public void ProjectBounds_RuntimeCorruptedExactInitializedLiteral_RecoversQuerySlotFromOriginalText()
    {
        // Real Ctrl+D Core shape: ExactInitializedEditableQueryValue selected the embedded-literal
        // line value 8, while OriginalText still carries 3/4/8.
        var component = ExactUnique(
            "Skills Fire 3 additional Projectiles for 4 seconds after\nyou consume a total of 8 Steel Shards",
            "Skills Fire <number> additional Projectiles for <number> seconds after\nyou consume a total of <number> Steel Shards",
            ["skills_fire_x_additional_projectiles_for_4_seconds_after_consuming_8_steel_ammo"],
            [8m],
            Translation(
                "skills_fire_x_additional_projectiles_for_4_seconds_after_consuming_8_steel_ammo",
                SingularVariant(
                    "Skills Fire {0} additional Projectile for 4 seconds after",
                    "you consume a total of 8 Steel Shards"),
                PluralVariant(
                    "Skills Fire {0} additional Projectiles for 4 seconds after",
                    "you consume a total of 8 Steel Shards")));
        component = component with
        {
            RequestedMinimum = 8m,
            RequestedMaximum = 8m,
            CanonicalNumericValues = [8m],
            ProviderFallbackNumericValues = [],
        };
        var candidate = Candidate(
            "Skills Fire # additional Projectile for 4 seconds after\nyou consume a total of 8 Steel Shards",
            "explicit.stat_1031404836");

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, candidate);
        Assert.Equal("SingularPluralInflectionScalar", bounds.ProjectionKind);
        Assert.Equal(ModifierBoundShape.Scalar, bounds.ValueBoundShape);
        Assert.Equal(3m, bounds.Minimum);
        Assert.Equal(3m, bounds.Maximum);

        var projected = PathOfExileTradeModifierBoundProjector.Project(component, candidate);
        Assert.Equal(3m, projected.RequestedMinimum);
        Assert.Equal(3m, projected.RequestedMaximum);
        Assert.True(projected.SupportsValueBounds);
    }

    [Fact]
    public void ProjectBounds_InflectionProvenButSlotUnrecoverable_FailClosedPresenceOnly()
    {
        var component = ExactUnique(
            "Skills Fire additional Projectiles for 4 seconds after\nyou consume a total of 8 Steel Shards",
            "Skills Fire additional Projectiles for <number> seconds after\nyou consume a total of <number> Steel Shards",
            ["synthetic_inflection_stat"],
            [8m],
            Translation(
                "synthetic_inflection_stat",
                SingularVariant(
                    "Skills Fire {0} additional Projectile for 4 seconds after",
                    "you consume a total of 8 Steel Shards"),
                PluralVariant(
                    "Skills Fire {0} additional Projectiles for 4 seconds after",
                    "you consume a total of 8 Steel Shards")));
        component = component with
        {
            RequestedMinimum = 8m,
            RequestedMaximum = 8m,
            CanonicalNumericValues = [8m],
            ObservedNumericValues = [8m],
            ProviderFallbackNumericValues = [],
            CanonicalSignature =
                "Skills Fire # additional Projectiles for # seconds after you consume a total of # Steel Shards",
            ProviderCanonicalSignature =
                "Skills Fire # additional Projectiles for # seconds after you consume a total of # Steel Shards",
            ProviderSearchSignatures =
            [
                "Skills Fire # additional Projectiles for # seconds after you consume a total of # Steel Shards",
            ],
            OriginalText =
                "Skills Fire additional Projectiles for 4 seconds after\nyou consume a total of 8 Steel Shards",
        };
        var candidate = Candidate(
            "Skills Fire # additional Projectile for 4 seconds after\nyou consume a total of 8 Steel Shards",
            "explicit.synthetic");

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, candidate);
        Assert.Equal("SingularPluralInflectionUnresolvedPresence", bounds.ProjectionKind);
        Assert.Equal(ModifierBoundShape.PresenceOnly, bounds.ValueBoundShape);
        Assert.Null(bounds.Minimum);
        Assert.Null(bounds.Maximum);
    }

    private static StatTranslationRecognitionEvidence Translation(
        string statId,
        params StatTranslationVariant[] variants) =>
        new()
        {
            Role = StatTranslationRecognitionRole.CurrentExact,
            CanonicalTranslation = new StatTranslationDefinition
            {
                Id = "test-translation:" + statId,
                StatIds = [statId],
                Language = "English",
                Variants = variants,
            },
            RecognizedTranslation = new StatTranslationDefinition
            {
                Id = "test-translation:" + statId,
                StatIds = [statId],
                Language = "English",
                Variants = variants,
            },
        };

    private static StatTranslationVariant SingularVariant(params string[] lines) =>
        new()
        {
            Conditions = [new StatTranslationCondition { Index = 0, MinValue = 1, MaxValue = 1 }],
            ValueFormats = ["#"],
            FormatLines = lines,
        };

    private static StatTranslationVariant PluralVariant(params string[] lines) =>
        new()
        {
            Conditions = [new StatTranslationCondition { Index = 0, MinValue = 2, MaxValue = null }],
            ValueFormats = ["#"],
            FormatLines = lines,
        };

    private static ResolvedSearchComponent ExactUnique(
        string original,
        string signature,
        IReadOnlyList<string> statIds,
        IReadOnlyList<decimal>? observed = null,
        StatTranslationRecognitionEvidence? translation = null) =>
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
            ResolvedModifierId = "unique-mod:inflection-test",
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
            TranslationRecognition = translation,
        };

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

    private static PathOfExileTradeStatMatchCandidate Candidate(string text, string id) =>
        new()
        {
            ProviderOrder = 0,
            GroupId = "explicit",
            GroupLabel = "explicit",
            Type = "explicit",
            StatId = id,
            Text = text,
            NormalizedTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeTemplate(text),
            LookupTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(text),
            ProviderKind = "explicit",
        };
}

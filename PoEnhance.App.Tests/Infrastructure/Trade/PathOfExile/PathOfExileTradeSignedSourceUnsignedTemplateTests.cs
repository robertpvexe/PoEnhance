using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4e Track B / TRADE.4e.3 — signed source display discovers unsigned Trade template
/// and retains the signed query scalar (Min=Max=source).
/// </summary>
public sealed class PathOfExileTradeSignedSourceUnsignedTemplateTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_TemperedSpirit_SignedSource_ResolvesUnsignedTradeSignedScalar()
    {
        var catalog = Catalog(
            Entry(
                "explicit.tempered-dex",
                "# Dexterity per 1 Dexterity on Allocated Passives in Radius",
                "explicit"));
        var component = ExactUnique(
            "-1 Dexterity per 1 Dexterity on Allocated Passives in Radius",
            "-<number> Dexterity per <number> Dexterity on Allocated Passives in Radius",
            [
                "local_unique_jewel_X_dexterity_per_1_dexterity_allocated_in_radius",
                "local_jewel_effect_base_radius",
            ],
            [-1m, 1m],
            UnsignedJewelTranslation(
                "local_unique_jewel_X_dexterity_per_1_dexterity_allocated_in_radius",
                "{0} Dexterity per 1 Dexterity on Allocated Passives in Radius"));
        var result = matcher.Match(component, catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.tempered-dex", result.ExactCandidate?.StatId);
        Assert.Equal(
            "# Dexterity per 1 Dexterity on Allocated Passives in Radius",
            result.ExactCandidate!.Text);
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            result.ExactCandidate.Text));
        Assert.Contains(" per 1 ", result.ExactCandidate.Text, StringComparison.Ordinal);

        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            component,
            result.ExactCandidate);
        Assert.Equal(ModifierBoundShape.Scalar, bounds.ValueBoundShape);
        Assert.Equal(-1m, bounds.Minimum);
        Assert.Equal(-1m, bounds.Maximum);
        Assert.Equal("SignedSourceUnsignedTradeMagnitude", bounds.ProjectionKind);
    }

    [Fact]
    public void Match_StrengthSibling_ResolvesUnsignedTradeMagnitude()
    {
        AssertSibling(
            "Strength",
            "local_unique_jewel_X_strength_per_1_strength_allocated_in_radius",
            "explicit.tempered-str");
    }

    [Fact]
    public void Match_IntelligenceSibling_ResolvesUnsignedTradeMagnitude()
    {
        AssertSibling(
            "Intelligence",
            "local_unique_jewel_X_intelligence_per_1_intelligence_allocated_in_radius",
            "explicit.tempered-int");
    }

    [Fact]
    public void Match_SyntheticSignedSourceUnsignedTemplate_ResolvesExactSignedScalar()
    {
        var catalog = Catalog(
            Entry("explicit.synthetic", "# Power per 1 Power on Allocated Passives in Radius", "explicit"));
        var component = ExactUnique(
            "-2 Power per 1 Power on Allocated Passives in Radius",
            "-<number> Power per <number> Power on Allocated Passives in Radius",
            ["synthetic_signed_unsigned_stat"],
            [-2m, 1m],
            UnsignedJewelTranslation(
                "synthetic_signed_unsigned_stat",
                "{0} Power per 1 Power on Allocated Passives in Radius"));
        var result = matcher.Match(component, catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            component,
            result.ExactCandidate!);
        Assert.Equal(-2m, bounds.Minimum);
        Assert.Equal(-2m, bounds.Maximum);
        Assert.Equal("SignedSourceUnsignedTradeMagnitude", bounds.ProjectionKind);
    }

    [Fact]
    public void Match_WithoutUnsignedFormatEvidence_RemainsNotFound()
    {
        var catalog = Catalog(
            Entry("explicit.trade", "# Power per 1 Power on Allocated Passives in Radius", "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "-2 Power per 1 Power on Allocated Passives in Radius",
                "-<number> Power per <number> Power on Allocated Passives in Radius",
                ["synthetic_signed_only"],
                [-2m, 1m],
                // Only a signed format exists — no unsigned family proof.
                Translation(
                    "synthetic_signed_only",
                    new StatTranslationVariant
                    {
                        Conditions =
                        [
                            new StatTranslationCondition { Index = 0 },
                        ],
                        ValueFormats = ["#"],
                        FormatLines = ["-{0} Power per 1 Power on Allocated Passives in Radius"],
                    })),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_IncompatibleTradeShape_DoesNotFalselyBridge()
    {
        // Trade publishes a different mechanic identity — must not Exact-map via sign stripping alone.
        var catalog = Catalog(
            Entry(
                "explicit.trade",
                "# Power Regenerated per Second",
                "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "-2 Power per 1 Power on Allocated Passives in Radius",
                "-<number> Power per <number> Power on Allocated Passives in Radius",
                ["synthetic_arity_stat"],
                [-2m, 1m],
                UnsignedJewelTranslation(
                    "synthetic_arity_stat",
                    "{0} Power per 1 Power on Allocated Passives in Radius")),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_MultipleUnsignedCandidates_RemainAmbiguous()
    {
        var catalog = Catalog(
            Entry("explicit.a", "# Power per 1 Power on Allocated Passives in Radius", "explicit", 0),
            Entry("explicit.b", "# Power per 1 Power on Allocated Passives in Radius", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                "-2 Power per 1 Power on Allocated Passives in Radius",
                "-<number> Power per <number> Power on Allocated Passives in Radius",
                ["synthetic_signed_unsigned_stat"],
                [-2m, 1m],
                UnsignedJewelTranslation(
                    "synthetic_signed_unsigned_stat",
                    "{0} Power per 1 Power on Allocated Passives in Radius")),
            catalog);

        Assert.True(
            result.Status is PathOfExileTradeStatMatchStatus.Ambiguous or
                PathOfExileTradeStatMatchStatus.ExactEquivalentSet);
        Assert.True(result.Candidates.Count >= 2 || result.ExactEquivalentCandidates.Count >= 2);
    }

    [Fact]
    public void ProjectBounds_TrueNegativeSignedTradeTemplate_RemainsNegative()
    {
        var result = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            ExactUnique(
                "-10 to maximum Energy Shield",
                "-<number> to maximum Energy Shield",
                ["genuine_negative_stat"],
                [-10m]),
            Candidate("-# to maximum Energy Shield", "explicit.neg"));

        Assert.True(result.IsFaithful);
        Assert.Equal(-10m, result.Minimum);
        Assert.Equal(-10m, result.Maximum);
        Assert.NotEqual("SignedSourceUnsignedTradeMagnitude", result.ProjectionKind);
    }

    [Fact]
    public void Project_SignedSourceUnsigned_PopulatesCanonicalNumericValuesWithSignedScalar()
    {
        var component = ExactUnique(
            "-1 Dexterity per 1 Dexterity on Allocated Passives in Radius",
            "-<number> Dexterity per <number> Dexterity on Allocated Passives in Radius",
            [
                "local_unique_jewel_X_dexterity_per_1_dexterity_allocated_in_radius",
                "local_jewel_effect_base_radius",
            ],
            [-1m, 1m],
            UnsignedJewelTranslation(
                "local_unique_jewel_X_dexterity_per_1_dexterity_allocated_in_radius",
                "{0} Dexterity per 1 Dexterity on Allocated Passives in Radius")) with
        {
            // Real Core draft leaves CanonicalNumericValues empty until Trade proves the slot.
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.Unsupported,
            CanonicalNumericValues = [],
            RequestedMinimum = null,
            RequestedMaximum = null,
        };

        var projected = PathOfExileTradeModifierBoundProjector.Project(
            component,
            Candidate(
                "# Dexterity per 1 Dexterity on Allocated Passives in Radius",
                "explicit.tempered-dex"));

        Assert.True(projected.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.Scalar, projected.ValueBoundShape);
        Assert.Equal([-1m], projected.CanonicalNumericValues);
        Assert.Equal(-1m, projected.RequestedMinimum);
        Assert.Equal(-1m, projected.RequestedMaximum);
    }

    [Fact]
    public void ProjectBounds_MoreLessNegate_DoesNotUseSignedUnsignedRule()
    {
        var result = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:4:0",
                OriginalText =
                    "Animated and Manifested Minions' Melee Strikes deal 50% less Splash Damage",
                CanonicalSignature =
                    "Animated and Manifested Minions' Melee Strikes deal <number>% less Splash Damage",
                ProviderCanonicalSignature =
                    "Animated and Manifested Minions' Melee Strikes deal <number>% more Splash Damage",
                ValueBoundShape = ModifierBoundShape.Scalar,
                SupportsValueBounds = true,
                ObservedNumericValues = [50m],
                CanonicalNumericValues = [-50m],
                ValueBoundTranslationHandlers = [["negate"]],
                DefaultBoundDirection = ModifierBoundDirection.Maximum,
                RequestedMaximum = -50m,
                ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                ResolvedModifierId = "unique-mod:more-less-control",
                ResolvedStatIds = ["grant_animated_minion_melee_splash_damage_+%_final_for_splash"],
                UniqueCatalogBlockIds = ["unique-block:test"],
                UniqueSourceObservationIds = ["pob-observation:test"],
                ParsedKind = ParsedModifierKind.Unique,
                UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            },
            Candidate(
                "Animated and Manifested Minions' Melee Strikes deal #% less Splash Damage",
                "explicit.less"));

        Assert.Equal("ReversingProviderMagnitudeScalar", result.ProjectionKind);
        Assert.Equal(50m, result.Minimum);
        Assert.Null(result.Maximum);
    }

    private void AssertSibling(string attribute, string statId, string tradeId)
    {
        var catalog = Catalog(
            Entry(
                tradeId,
                $"# {attribute} per 1 {attribute} on Allocated Passives in Radius",
                "explicit"));
        var component = ExactUnique(
            $"-1 {attribute} per 1 {attribute} on Allocated Passives in Radius",
            $"-<number> {attribute} per <number> {attribute} on Allocated Passives in Radius",
            [statId, "local_jewel_effect_base_radius"],
            [-1m, 1m],
            UnsignedJewelTranslation(
                statId,
                $"{{0}} {attribute} per 1 {attribute} on Allocated Passives in Radius"));
        var result = matcher.Match(component, catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal(tradeId, result.ExactCandidate?.StatId);
        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            component,
            result.ExactCandidate!);
        Assert.Equal(-1m, bounds.Minimum);
        Assert.Equal(-1m, bounds.Maximum);
        Assert.Equal("SignedSourceUnsignedTradeMagnitude", bounds.ProjectionKind);
    }

    private static StatTranslationRecognitionEvidence UnsignedJewelTranslation(
        string statId,
        string formatLine) =>
        Translation(
            statId,
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                ValueFormats = ["#"],
                FormatLines = [formatLine],
            });

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
            ResolvedModifierId = "unique-mod:signed-unsigned-test",
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

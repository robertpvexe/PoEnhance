using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4e Track A — same-family helper-word condition branch discovers Trade canonical form.
/// </summary>
public sealed class PathOfExileTradeHelperWordBranchGrammarTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_HelperWordBranch_WithSameFamilySibling_ResolvesExactScalar()
    {
        var catalog = Catalog(
            Entry(
                "explicit.hungry",
                "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume # additional Uncorrupted Support Gems",
                "explicit"));
        var component = ExactUnique(
            "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume 4 Uncorrupted Support Gems\nHas not Consumed any Gems",
            "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume <number> Uncorrupted Support Gems\nHas not Consumed any Gems",
            ["local_unique_hungry_loop_number_of_gems_to_consume", "local_unique_hungry_loop_has_consumed_gem"],
            [4m],
            HungryFamilyTranslation());
        var result = matcher.Match(component, catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.hungry", result.ExactCandidate?.StatId);
        Assert.Contains("additional", result.ExactCandidate!.Text, StringComparison.Ordinal);
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
    public void Match_SyntheticHelperWordSibling_ResolvesExact()
    {
        var catalog = Catalog(
            Entry(
                "explicit.synthetic",
                "Emits Signal\nCan Trigger # additional Charges",
                "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "Emits Signal\nCan Trigger 3 Charges\nHas not Triggered",
                "Emits Signal\nCan Trigger <number> Charges\nHas not Triggered",
                ["synthetic_helper_word_stat", "synthetic_helper_companion"],
                [3m],
                Translation(
                    ["synthetic_helper_word_stat", "synthetic_helper_companion"],
                    NoHelperWithCompanion(
                        "Emits Signal",
                        "Can Trigger {0} Charges",
                        "Has not Triggered"),
                    WithHelper(
                        "Emits Signal",
                        "Can Trigger {0} additional Charges"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.synthetic", result.ExactCandidate?.StatId);
        Assert.Contains("additional", result.ExactCandidate!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_WithoutHelperWordSiblingEvidence_RemainsNotFound()
    {
        var catalog = Catalog(
            Entry(
                "explicit.trade",
                "Emits Signal\nCan Trigger # additional Charges",
                "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "Emits Signal\nCan Trigger 3 Charges\nHas not Triggered",
                "Emits Signal\nCan Trigger <number> Charges\nHas not Triggered",
                ["synthetic_helper_only_source"],
                [3m],
                Translation(
                    ["synthetic_helper_only_source"],
                    NoHelperWithCompanion(
                        "Emits Signal",
                        "Can Trigger {0} Charges",
                        "Has not Triggered"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_WrongTranslationFamily_Rejected()
    {
        var catalog = Catalog(
            Entry(
                "explicit.trade",
                "Emits Signal\nCan Trigger # additional Charges",
                "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "Emits Signal\nCan Trigger 3 Charges\nHas not Triggered",
                "Emits Signal\nCan Trigger <number> Charges\nHas not Triggered",
                ["other_family_stat"],
                [3m],
                Translation(
                    ["other_family_stat"],
                    NoHelperWithCompanion(
                        "Other Prefix",
                        "Can Trigger {0} Charges",
                        "Has not Triggered"),
                    WithHelper(
                        "Other Prefix",
                        "Can Trigger {0} additional Charges"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_IncompatibleAritySibling_FailClosed()
    {
        var catalog = Catalog(
            Entry("explicit.trade", "Emits Signal\nCan Trigger # additional Charges", "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "Emits Signal\nCan Trigger 3 Charges\nHas not Triggered",
                "Emits Signal\nCan Trigger <number> Charges\nHas not Triggered",
                ["synthetic_arity_stat", "synthetic_arity_companion"],
                [3m],
                Translation(
                    ["synthetic_arity_stat", "synthetic_arity_companion"],
                    NoHelperWithCompanion(
                        "Emits Signal",
                        "Can Trigger {0} Charges",
                        "Has not Triggered"),
                    // Trade sibling gains a second query slot — incompatible FilterArity.
                    new StatTranslationVariant
                    {
                        Conditions =
                        [
                            new StatTranslationCondition { Index = 0, MinValue = 2, MaxValue = null },
                            new StatTranslationCondition { Index = 1 },
                        ],
                        ValueFormats = ["#", "#"],
                        FormatLines =
                        [
                            "Emits Signal",
                            "Can Trigger {0} additional Charges and {1} Bursts",
                        ],
                    })),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_MultipleHelperWordCandidates_RemainAmbiguous()
    {
        var catalog = Catalog(
            Entry(
                "explicit.a",
                "Emits Signal\nCan Trigger # additional Charges",
                "explicit",
                0),
            Entry(
                "explicit.b",
                "Emits Signal\nCan Trigger # additional Charges",
                "explicit",
                1));

        var result = matcher.Match(
            ExactUnique(
                "Emits Signal\nCan Trigger 3 Charges\nHas not Triggered",
                "Emits Signal\nCan Trigger <number> Charges\nHas not Triggered",
                ["synthetic_helper_word_stat", "synthetic_helper_companion"],
                [3m],
                Translation(
                    ["synthetic_helper_word_stat", "synthetic_helper_companion"],
                    NoHelperWithCompanion(
                        "Emits Signal",
                        "Can Trigger {0} Charges",
                        "Has not Triggered"),
                    WithHelper(
                        "Emits Signal",
                        "Can Trigger {0} additional Charges"))),
            catalog);

        Assert.True(
            result.Status is PathOfExileTradeStatMatchStatus.Ambiguous or
                PathOfExileTradeStatMatchStatus.ExactEquivalentSet);
        Assert.True(result.Candidates.Count >= 2 || result.ExactEquivalentCandidates.Count >= 2);
    }

    [Fact]
    public void Match_ArbitraryOmittedWordWithoutBranchProof_RemainsNotFound()
    {
        var catalog = Catalog(
            Entry("explicit.trade", "Skills Fire # additional Projectiles", "explicit"));

        // Source omits "additional" but translation has only the Trade form — no sibling proof.
        var result = matcher.Match(
            ExactUnique(
                "Skills Fire 3 Projectiles",
                "Skills Fire <number> Projectiles",
                ["unrelated_projectile_stat"],
                [3m],
                Translation(
                    ["unrelated_projectile_stat"],
                    new StatTranslationVariant
                    {
                        Conditions =
                        [
                            new StatTranslationCondition { Index = 0, MinValue = 1, MaxValue = null },
                        ],
                        ValueFormats = ["#"],
                        FormatLines = ["Skills Fire {0} additional Projectiles"],
                    })),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    private static StatTranslationRecognitionEvidence HungryFamilyTranslation() =>
        Translation(
            ["local_unique_hungry_loop_number_of_gems_to_consume", "local_unique_hungry_loop_has_consumed_gem"],
            NoHelperWithCompanion(
                "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level",
                "Can Consume {0} Uncorrupted Support Gems",
                "Has not Consumed any Gems"),
            WithHelper(
                "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level",
                "Can Consume {0} additional Uncorrupted Support Gems"));

    private static StatTranslationRecognitionEvidence Translation(
        IReadOnlyList<string> statIds,
        params StatTranslationVariant[] variants) =>
        new()
        {
            Role = StatTranslationRecognitionRole.CurrentExact,
            CanonicalTranslation = new StatTranslationDefinition
            {
                Id = "test-translation:" + string.Join('|', statIds),
                StatIds = statIds.ToArray(),
                Language = "English",
                Variants = variants,
            },
            RecognizedTranslation = new StatTranslationDefinition
            {
                Id = "test-translation:" + string.Join('|', statIds),
                StatIds = statIds.ToArray(),
                Language = "English",
                Variants = variants,
            },
        };

    private static StatTranslationVariant NoHelperWithCompanion(params string[] lines) =>
        new()
        {
            Conditions =
            [
                new StatTranslationCondition { Index = 0, MinValue = 2, MaxValue = null },
                new StatTranslationCondition { Index = 1, MinValue = 0, MaxValue = 0 },
            ],
            ValueFormats = ["#", "ignore"],
            FormatLines = lines,
        };

    private static StatTranslationVariant WithHelper(params string[] lines) =>
        new()
        {
            Conditions =
            [
                new StatTranslationCondition { Index = 0, MinValue = 2, MaxValue = null },
                new StatTranslationCondition { Index = 1 },
            ],
            ValueFormats = ["#", "ignore"],
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
            ResolvedModifierId = "unique-mod:helper-word-test",
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
}

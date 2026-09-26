using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4a — Exact Unique chance StatIds may omit Trade's generic "#% chance to" wrapper;
/// discovery projects the wrapper from structural StatId evidence only.
/// </summary>
public sealed class PathOfExileTradeOmittedChanceWrapperTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_OmittedChanceWrapper_WithChanceStatId_ResolvesExactTradeTemplate()
    {
        var catalog = Catalog(
            Entry(
                "explicit.stat_guardian",
                "#% chance to Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                "explicit",
                0));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                signature: "Trigger Level <number> Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                statIds:
                [
                    "animate_guardian_and_weapon_track_on_kill",
                    "local_display_trigger_level_20_animate_guardian_weapon_on_guardian_kill_%_chance",
                ]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_guardian", result.ExactCandidate?.StatId);
        Assert.StartsWith(
            "#% chance to Trigger Level",
            result.ExactCandidate!.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectBounds_OmittedChanceWrapper_WithEmbeddedLevelLiteral_IsPresenceOnly()
    {
        const string action =
            "Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy";
        const string chance = $"#% chance to {action}";
        var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            ExactUniqueChance(
                original: action,
                signature: "Trigger Level <number> Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                statIds:
                [
                    "animate_guardian_and_weapon_track_on_kill",
                    "local_display_trigger_level_20_animate_guardian_weapon_on_guardian_kill_%_chance",
                ],
                observed: [20m]),
            new PathOfExileTradeStatMatchCandidate
            {
                ProviderOrder = 0,
                GroupId = "explicit",
                GroupLabel = "explicit",
                Type = "explicit",
                StatId = "explicit.stat_guardian",
                Text = chance,
                NormalizedTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeTemplate(chance),
                LookupTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(chance),
                ProviderKind = "explicit",
            });

        Assert.True(projection.IsFaithful);
        Assert.Equal(ModifierBoundShape.PresenceOnly, projection.ValueBoundShape);
        Assert.Null(projection.Minimum);
        Assert.Null(projection.Maximum);
        Assert.Equal("OmittedChanceWrapperPresence", projection.ProjectionKind);
    }

    [Fact]
    public void Match_SyntheticChanceStatId_OmittedWrapper_ResolvesExact()
    {
        var catalog = Catalog(
            Entry(
                "explicit.synthetic",
                "#% chance to Trigger Level # Test Effect on Kill",
                "explicit",
                0));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "Trigger Level 5 Test Effect on Kill",
                signature: "Trigger Level <number> Test Effect on Kill",
                statIds: ["local_display_trigger_level_5_test_effect_on_kill_%_chance"]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.synthetic", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_OmittedWrapper_WithoutChanceStatIdEvidence_RemainsNotFound()
    {
        var catalog = Catalog(
            Entry(
                "explicit.stat_guardian",
                "#% chance to Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                "explicit",
                0));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                signature: "Trigger Level <number> Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                // Magnitude-style _+% is NOT chance-wrapper evidence.
                statIds: ["local_display_trigger_level_20_animate_guardian_weapon_on_guardian_kill_+%"]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_UnrelatedStatContainingChanceWord_IsNotRewritten()
    {
        var catalog = Catalog(
            Entry("explicit.block", "+#% Chance to Block", "explicit", 0),
            Entry("explicit.chance_wrapper", "#% chance to +#% Chance to Block", "explicit", 1));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "+12% Chance to Block",
                signature: "+<number>% Chance to Block",
                // Display contains "Chance" but StatId has no chance-wrapper token.
                statIds: ["local_additional_block_chance_%"]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.block", result.ExactCandidate?.StatId);
        Assert.DoesNotContain(
            result.ExactCandidate!.Text,
            "chance to +",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Match_OmittedHelperWordOtherThanChance_Unchanged()
    {
        var catalog = Catalog(
            Entry(
                "explicit.hungry",
                "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume # additional Uncorrupted Support Gems",
                "explicit",
                0));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume 4 Uncorrupted Support Gems",
                signature: "Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level\nCan Consume <number> Uncorrupted Support Gems",
                statIds: ["local_unique_hungry_loop_number_of_gems_to_consume"]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_MultipleCompatibleChanceCandidates_RemainAmbiguous()
    {
        var catalog = Catalog(
            Entry(
                "explicit.a",
                "#% chance to Trigger Level # Test Effect on Kill",
                "explicit",
                0),
            Entry(
                "explicit.b",
                "#% chance to Trigger Level # Test Effect on Kill",
                "explicit",
                1));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "Trigger Level 5 Test Effect on Kill",
                signature: "Trigger Level <number> Test Effect on Kill",
                statIds: ["local_display_trigger_test_effect_on_kill_%_chance"]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.ExactEquivalentSet, result.Status);
        Assert.Equal(2, result.ExactEquivalentCandidates.Count);
    }

    [Fact]
    public void Match_OmittedChanceWrapper_WrongKindRejected()
    {
        var catalog = Catalog(
            Entry(
                "scourge.trigger",
                "#% chance to Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                "scourge",
                0));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                signature: "Trigger Level <number> Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                statIds:
                [
                    "local_display_trigger_level_20_animate_guardian_weapon_on_guardian_kill_%_chance",
                ]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_OmittedChanceWrapper_MatchingEmbeddedLevelLiteral_Selected()
    {
        // Official Trade distinguishes the siblings by kill-target wording as well as level.
        var catalog = Catalog(
            Entry(
                "explicit.level18",
                "#% chance to Trigger Level 18 Animate Guardian's Weapon when Animated Weapon Kills an Enemy",
                "explicit",
                0),
            Entry(
                "explicit.level20",
                "#% chance to Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                "explicit",
                1));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                signature: "Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                statIds:
                [
                    "local_display_trigger_level_20_animate_guardian_weapon_on_guardian_kill_%_chance",
                ]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.level20", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_OmittedChanceWrapper_UnderSpecifiedLevel_RemainsAmbiguous()
    {
        var catalog = Catalog(
            Entry(
                "explicit.level18",
                "#% chance to Trigger Level 18 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                "explicit",
                0),
            Entry(
                "explicit.level20",
                "#% chance to Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                "explicit",
                1));

        var result = matcher.Match(
            ExactUniqueChance(
                original: "Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                signature: "Trigger Level <number> Animate Guardian's Weapon when Animated Guardian Kills an Enemy",
                statIds:
                [
                    "local_display_trigger_level_20_animate_guardian_weapon_on_guardian_kill_%_chance",
                ]),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Candidates.Count);
    }

    private static ResolvedSearchComponent ExactUniqueChance(
        string original,
        string signature,
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
            ProviderSearchSignatures = [signature],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "unique-mod:chance-wrapper-test",
            ResolvedStatIds = statIds.ToArray(),
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            IsSearchable = true,
            SupportsValueBounds = signature.Contains("<number>", StringComparison.Ordinal) ||
                signature.Any(char.IsDigit),
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = observed?.ToArray() ?? [],
            ProviderFallbackNumericValues = observed?.ToArray() ?? [],
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

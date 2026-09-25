using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.2 Track B — embedded literal constant / FilterArity Ambiguous disambiguation.
/// </summary>
public sealed class PathOfExileTradeEmbeddedConstantDisambiguationTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_HyrriStyle_ParameterizedSiblingPreferredOverFixedLiteralWhenSourceHasDynamicSlot()
    {
        var catalog = Catalog(
            Entry("explicit.literal", "Precision has 100% increased Mana Reservation Efficiency", "explicit", 0),
            Entry("explicit.param", "Precision has #% increased Mana Reservation Efficiency", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "Precision has 100% increased Mana Reservation Efficiency",
                signature: "Precision has <number>% increased Mana Reservation Efficiency",
                statId: "precision_mana_reservation_efficiency_+%"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.param", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_DeathsHandStyle_LiteralDurationSelected()
    {
        var catalog = Catalog(
            Entry("explicit.two", "Gain Unholy Might for 2 seconds on Critical Strike", "explicit", 0),
            Entry("explicit.four", "Gain Unholy Might for 4 seconds on Critical Strike", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "Gain Unholy Might for 4 seconds on Critical Strike",
                signature: "Gain Unholy Might for 4 seconds on Critical Strike",
                statId: "gain_unholy_might_for_4_seconds_on_crit"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.four", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_DarkscornStyle_EmbeddedMoreDamageConstantSelected()
    {
        var catalog = Catalog(
            Entry("explicit.one_hundred", "#% chance for Poisons inflicted with this Weapon to deal 100% more Damage", "explicit", 0),
            Entry("explicit.three_hundred", "#% chance for Poisons inflicted with this Weapon to deal 300% more Damage", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "60% chance for Poisons inflicted with this Weapon to deal 300% more Damage",
                signature: "<number>% chance for Poisons inflicted with this Weapon to deal 300% more Damage",
                statId: "local_chance_for_poison_damage_+300%_final_inflicted_with_weapon"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.three_hundred", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_ReplicaWindripperStyle_LiteralSelectedOverParameterized()
    {
        var catalog = Catalog(
            Entry("explicit.param", "Enemies Frozen by you take #% increased Damage", "explicit", 0),
            Entry("explicit.literal", "Enemies Frozen by you take 20% increased Damage", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "Enemies Frozen by you take 20% increased Damage",
                signature: "Enemies Frozen by you take 20% increased Damage",
                statId: "frozen_monsters_take_increased_damage"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.literal", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_HoWaStyle_Per25DivisorSelected()
    {
        var catalog = Catalog(
            Entry("explicit.per10", "#% increased Attack Speed per 10 Dexterity", "explicit", 0),
            Entry("explicit.per25", "#% increased Attack Speed per 25 Dexterity", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "1% increased Attack Speed per 25 Dexterity",
                signature: "<number>% increased Attack Speed per 25 Dexterity",
                statId: "attack_speed_+%_per_25_dex"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.per25", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_WhisperingIceStyle_Per10DivisorSelected()
    {
        var catalog = Catalog(
            Entry("explicit.per10", "#% increased Spell Damage per 10 Intelligence", "explicit", 0),
            Entry("explicit.per16", "#% increased Spell Damage per 16 Intelligence", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "1% increased Spell Damage per 10 Intelligence",
                signature: "<number>% increased Spell Damage per 10 Intelligence",
                statId: "spell_damage_+%_per_10_int"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.per10", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_FractalThoughtsStyle_Per10LifeDivisorSelected()
    {
        var catalog = Catalog(
            Entry("explicit.per10", "+# to Maximum Life per 10 Intelligence", "explicit", 0),
            Entry("explicit.per2", "+# to Maximum Life per 2 Intelligence", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "+1 to Maximum Life per 10 Intelligence",
                signature: "+<number> to Maximum Life per 10 Intelligence",
                statId: "maximum_life_per_10_intelligence"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.per10", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_UnderSpecifiedDivisor_RemainsAmbiguous()
    {
        var catalog = Catalog(
            Entry("explicit.per10", "#% increased Attack Speed per 10 Dexterity", "explicit", 0),
            Entry("explicit.per25", "#% increased Attack Speed per 25 Dexterity", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "1% increased Attack Speed per 25 Dexterity",
                signature: "<number>% increased Attack Speed per <number> Dexterity",
                statId: "attack_speed_+%_per_unknown_dex"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Match_ProvenLiteralWithNoCompatibleCandidate_IsNotFound()
    {
        var catalog = Catalog(
            Entry("explicit.per10", "#% increased Attack Speed per 10 Dexterity", "explicit", 0),
            Entry("explicit.per16", "#% increased Attack Speed per 16 Dexterity", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "1% increased Attack Speed per 25 Dexterity",
                signature: "<number>% increased Attack Speed per 25 Dexterity",
                statId: "attack_speed_+%_per_25_dex"),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_MjolnerStyleZeroArityPresence_DoesNotSelectParameterizedSiblingFromLiteral()
    {
        var catalog = Catalog(
            Entry("explicit.trigger", "Trigger a Socketed Lightning Spell on Hit", "explicit", 0),
            Entry("explicit.param", "Trigger a Socketed Lightning Spell on Hit with #% chance", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                original: "Trigger a Socketed Lightning Spell on Hit, with a 0.25 second Cooldown",
                signature: "Trigger a Socketed Lightning Spell on Hit",
                statId: "local_display_trigger_socketed_lightning_spells_on_hit_%_chance") with
            {
                ObservedNumericValues = [0.25m],
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
            },
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.trigger", result.ExactCandidate?.StatId);
        Assert.Equal(
            0,
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(result.ExactCandidate!.Text));
    }

    [Fact]
    public void Derive_PreservesArityAndEmbeddedLiterals()
    {
        var signature = PathOfExileTradeStatEmbeddedConstantSignature.Derive(
            "#% increased Attack Speed per 25 Dexterity");

        Assert.Equal(1, signature.FilterArity);
        Assert.Equal([25m], signature.EmbeddedLiterals);
    }

    private static ResolvedSearchComponent ExactUnique(
        string original,
        string signature,
        string statId) =>
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
            ResolvedModifierId = "unique-mod:test",
            ResolvedStatIds = [statId],
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            IsSearchable = true,
            SupportsValueBounds = signature.Contains("<number>", StringComparison.Ordinal),
            ValueBoundShape = signature.Contains("<number>", StringComparison.Ordinal)
                ? ModifierBoundShape.Scalar
                : ModifierBoundShape.PresenceOnly,
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

using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class UniqueMechanicalConflictClassifierTests
{
    [Fact]
    public void Classify_PermyriadVersusDeprecatedPercent_WinsOverBroadSameText()
    {
        var kind = UniqueMechanicalConflictClassifier.Classify(
        [
            Candidate(
                "mod.current",
                ["local_life_leech_from_physical_damage_permyriad"],
                handlers: ["divide_by_one_hundred"]),
            Candidate(
                "mod.legacy",
                ["old_do_not_use_local_life_leech_from_physical_damage_%"],
                handlers: ["old_leech_percent"]),
        ]);

        Assert.Equal(
            UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent,
            kind);
    }

    [Fact]
    public void Classify_LevelVersusChance_WinsOverSameDisplayText()
    {
        var kind = UniqueMechanicalConflictClassifier.Classify(
        [
            Candidate("mod.level", ["curse_on_hit_level_temporal_chains"]),
            Candidate("mod.chance", ["curse_on_hit_%_temporal_chains"]),
        ]);

        Assert.Equal(UniqueMechanicalConflictKind.LevelVsChanceOnHit, kind);
    }

    [Fact]
    public void Classify_EfficiencyPlusVersusInverse_IsInverseLegacyHandlerEncoding()
    {
        var kind = UniqueMechanicalConflictClassifier.Classify(
        [
            Candidate("mod.plus", ["herald_of_ice_mana_reservation_efficiency_+%"]),
            Candidate("mod.inverse", ["herald_of_ice_mana_reservation_efficiency_-2%_per_1"]),
        ]);

        Assert.Equal(UniqueMechanicalConflictKind.InverseLegacyHandlerEncoding, kind);
    }

    [Fact]
    public void Classify_DeprecatedWithoutPermyriad_IsCurrentVsDeprecatedSourceMechanics()
    {
        var kind = UniqueMechanicalConflictClassifier.Classify(
        [
            Candidate("mod.current", ["modern_fire_resistance_+%"]),
            Candidate("mod.legacy", ["old_do_not_use_fire_resistance_+%"]),
        ]);

        Assert.Equal(UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics, kind);
    }

    [Fact]
    public void Classify_DistinctStatVectorsWithoutSpecialMarkers_IsSameDisplayTextDifferentStatIds()
    {
        var kind = UniqueMechanicalConflictClassifier.Classify(
        [
            Candidate("mod.one", ["first_critical"]),
            Candidate("mod.two", ["second_critical"]),
        ]);

        Assert.Equal(UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds, kind);
    }

    [Fact]
    public void Classify_DoesNotAssignTradeDuplicateKindWithoutTradeEvidence()
    {
        var kind = UniqueMechanicalConflictClassifier.Classify(
        [
            Candidate("mod.one", ["alpha_stat"]),
            Candidate("mod.two", ["beta_stat"]),
        ]);

        Assert.NotEqual(
            UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIdsWithTradeDuplicates,
            kind);
    }

    [Fact]
    public void BuildEncodingMarkers_IsDeterministicAndSorted()
    {
        var first = UniqueMechanicalConflictClassifier.BuildEncodingMarkers(
            "old_do_not_use_mod",
            ["local_life_leech_from_physical_damage_permyriad"],
            ["divide_by_one_hundred", "negate"]);
        var second = UniqueMechanicalConflictClassifier.BuildEncodingMarkers(
            "old_do_not_use_mod",
            ["local_life_leech_from_physical_damage_permyriad"],
            ["negate", "divide_by_one_hundred"]);

        Assert.Equal(first, second);
        Assert.Contains(UniqueMechanicalConflictClassifier.MarkerDeprecatedName, first);
        Assert.Contains(UniqueMechanicalConflictClassifier.MarkerPermyriad, first);
        Assert.Contains(UniqueMechanicalConflictClassifier.MarkerHandlerNegate, first);
    }

    private static UniqueMechanicalConflictCandidate Candidate(
        string modifierId,
        IReadOnlyList<string> statIds,
        IReadOnlyList<string>? handlers = null)
    {
        var resolvedHandlers = handlers ?? [];
        return new UniqueMechanicalConflictCandidate
        {
            ModifierId = modifierId,
            StatIds = statIds,
            Handlers = resolvedHandlers,
            EncodingMarkers = UniqueMechanicalConflictClassifier.BuildEncodingMarkers(
                modifierId,
                statIds,
                resolvedHandlers),
        };
    }
}

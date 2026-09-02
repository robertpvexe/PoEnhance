using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class NumericQueryRoleClassifierTests
{
    [Theory]
    [InlineData("local_display_socketed_gems_supported_by_level_x_spell_echo")]
    [InlineData("local_display_grants_level_x_clarity")]
    [InlineData("local_display_trigger_level_x_void_gaze_on_skill_use")]
    [InlineData("local_socketed_gem_level_+")]
    [InlineData("skill_gem_level_+")]
    public void Classify_SkillGemLevelStatIds_ReturnsSkillGemLevelThreshold(string statId)
    {
        var role = ClassifySingleScalar(statId, "Example {0}");

        Assert.Equal(NumericQueryRole.SkillGemLevelThreshold, role);
    }

    [Theory]
    [InlineData("maximum_life_per_level")]
    [InlineData("chaos_damage_per_level")]
    [InlineData("mana_regeneration_per_level")]
    public void Classify_CharacterPerLevelStatIds_ReturnsOrdinaryScalar(string statId)
    {
        var role = ClassifySingleScalar(
            statId,
            "+{0} Maximum Life per Level");

        Assert.Equal(NumericQueryRole.OrdinaryScalar, role);
    }

    [Fact]
    public void Classify_CoupledRatioSingleIndex_ReturnsCoupledRatio()
    {
        var role = NumericQueryRoleClassifier.Classify(
            ["chaos_resistance_per_cold_resistance"],
            [0],
            Variant("{0}% to Chaos Resistance per 1% Cold Resistance", ["#"]),
            ModifierBoundShape.Scalar,
            isSupported: true);

        Assert.Equal(NumericQueryRole.CoupledRatio, role);
    }

    [Fact]
    public void Classify_MultiIndexTriggerChance_ReturnsUnknown()
    {
        var role = NumericQueryRoleClassifier.Classify(
            ["trigger_chance_and_level"],
            [0, 1],
            Variant(
                "{0}% chance to Trigger Level {1} Summon Raging Spirit on Kill",
                ["#", "#"]),
            ModifierBoundShape.Scalar,
            isSupported: true);

        Assert.Equal(NumericQueryRole.Unknown, role);
    }

    [Fact]
    public void Classify_PresenceOnly_ReturnsPresenceOnly()
    {
        var role = NumericQueryRoleClassifier.Classify(
            ["presence_stat"],
            [],
            Variant("You can apply an additional Curse", []),
            ModifierBoundShape.PresenceOnly,
            isSupported: false);

        Assert.Equal(NumericQueryRole.PresenceOnly, role);
    }

    [Fact]
    public void Classify_UnsupportedShape_ReturnsUnknown()
    {
        var role = NumericQueryRoleClassifier.Classify(
            ["test_stat"],
            [0],
            Variant("Example {0}", ["#"]),
            ModifierBoundShape.Unsupported,
            isSupported: false);

        Assert.Equal(NumericQueryRole.Unknown, role);
    }

    private static NumericQueryRole ClassifySingleScalar(string statId, string formatLine) =>
        NumericQueryRoleClassifier.Classify(
            [statId],
            [0],
            Variant(formatLine, ["#"]),
            ModifierBoundShape.Scalar,
            isSupported: true);

    private static StatTranslationVariant Variant(string formatLine, IReadOnlyList<string> valueFormats) =>
        new()
        {
            Conditions = valueFormats
                .Select((_, index) => new StatTranslationCondition { Index = index })
                .ToArray(),
            ValueFormats = valueFormats,
            IndexHandlers = valueFormats
                .Select((_, index) => new StatTranslationIndexHandler { Index = index })
                .ToArray(),
            FormatLines = [formatLine],
        };
}

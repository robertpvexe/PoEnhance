using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class LogicalEffectItemTotalCalculatorTests
{
    [Fact]
    public void TryCompute_IncludesUnsplitImplicitCompositeAndCompactAllElemental()
    {
        // Viper Knuckle shape: implicit Fire+Lightning 15 + all Elemental 14 => Lightning/Fire 29, Cold 14.
        var implicitPair = Component(
            "fire_and_lightning_damage_resistance_%",
            "+15% to Fire and Lightning Resistances",
            15m,
            ParsedModifierKind.Implicit);
        var allElemental = Component(
            "base_resist_all_elements_%",
            "+14% to all Elemental Resistances",
            14m,
            ParsedModifierKind.Suffix) with
        {
            LogicalContributions =
            [
                new SearchComponentLogicalContribution
                {
                    ContributionId = "c-fire",
                    LogicalStatId = "base_fire_damage_resistance_%",
                    DisplayText = "+14% to Fire Resistance",
                    CanonicalSignature = "+<number>% to Fire Resistance",
                    PhysicalSourceIdentity = "phys:1:0:logic:base_fire_damage_resistance_%",
                    CanonicalNumericValues = [14m],
                },
            ],
        };

        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, allElemental],
            "base_lightning_damage_resistance_%",
            out var lightning));
        Assert.Equal(29m, lightning);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, allElemental],
            "base_fire_damage_resistance_%",
            out var fire));
        Assert.Equal(29m, fire);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, allElemental],
            "base_cold_damage_resistance_%",
            out var cold));
        Assert.Equal(14m, cold);
        Assert.True(LogicalEffectItemTotalCalculator.TryComputeTotalElementalResistance(
            [implicitPair, allElemental],
            out var elemental));
        // Fire 29 + Cold 14 + Lightning 29 = 72 (Chaos excluded).
        Assert.Equal(72m, elemental);
    }

    [Fact]
    public void TryComputeTotalElementalResistance_ImplicitColdLightningPlusColdPlusChaos_Is78()
    {
        // Implicit +15 Cold+Lightning, ordinary +48 Cold, ordinary +35 Chaos => Cold 63, Lightning 15,
        // Elemental 78; Chaos ignored.
        var implicitPair = Component(
            "cold_and_lightning_damage_resistance_%",
            "+15% to Cold and Lightning Resistances",
            15m,
            ParsedModifierKind.Implicit);
        var cold = Component(
            "base_cold_damage_resistance_%",
            "+48% to Cold Resistance",
            48m,
            ParsedModifierKind.Suffix);
        var chaos = Component(
            "base_chaos_damage_resistance_%",
            "+35% to Chaos Resistance",
            35m,
            ParsedModifierKind.Suffix);

        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, cold, chaos],
            "base_cold_damage_resistance_%",
            out var coldTotal));
        Assert.Equal(63m, coldTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, cold, chaos],
            "base_lightning_damage_resistance_%",
            out var lightningTotal));
        Assert.Equal(15m, lightningTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryComputeTotalElementalResistance(
            [implicitPair, cold, chaos],
            out var elemental));
        Assert.Equal(78m, elemental);
    }

    [Fact]
    public void TryComputeTotalElementalResistance_AllElementalAlone_Is45()
    {
        var allElemental = Component(
            "base_resist_all_elements_%",
            "+15% to all Elemental Resistances",
            15m,
            ParsedModifierKind.Suffix);
        Assert.True(LogicalEffectItemTotalCalculator.TryComputeTotalElementalResistance(
            [allElemental],
            out var elemental));
        Assert.Equal(45m, elemental);
    }

    [Fact]
    public void TryComputeTotalElementalResistance_CraftedFireChaosAlone_Is13()
    {
        var sharedPhysical = "phys:0:0:modifier:0:0";
        var fire = Component(
            "base_fire_damage_resistance_%",
            "+13% to Fire Resistance",
            13m,
            ParsedModifierKind.Suffix) with
        {
            PhysicalSourceIdentity = sharedPhysical + ":logic:base_fire_damage_resistance_%",
        };
        var chaos = Component(
            "base_chaos_damage_resistance_%",
            "+13% to Chaos Resistance",
            13m,
            ParsedModifierKind.Suffix) with
        {
            PhysicalSourceIdentity = sharedPhysical + ":logic:base_chaos_damage_resistance_%",
        };
        Assert.True(LogicalEffectItemTotalCalculator.TryComputeTotalElementalResistance(
            [fire, chaos],
            out var elemental));
        Assert.Equal(13m, elemental);
    }

    [Fact]
    public void TryCompute_PainCircleShape_Fire35_Chaos15_Cold28_Lightning13()
    {
        var implicitPair = Component(
            "cold_and_lightning_damage_resistance_%",
            "+13% to Cold and Lightning Resistances",
            13m,
            ParsedModifierKind.Implicit);
        var fire = Component(
            "base_fire_damage_resistance_%",
            "+35% to Fire Resistance",
            35m,
            ParsedModifierKind.Suffix);
        var chaos = Component(
            "base_chaos_damage_resistance_%",
            "+15% to Chaos Resistance",
            15m,
            ParsedModifierKind.Suffix) with
        {
            PhysicalSourceIdentity = "phys:craft:logic:base_chaos_damage_resistance_%",
        };
        var cold = Component(
            "base_cold_damage_resistance_%",
            "+15% to Cold Resistance",
            15m,
            ParsedModifierKind.Suffix);

        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, fire, chaos, cold],
            "base_fire_damage_resistance_%",
            out var fireTotal));
        Assert.Equal(35m, fireTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, fire, chaos, cold],
            "base_chaos_damage_resistance_%",
            out var chaosTotal));
        Assert.Equal(15m, chaosTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, fire, chaos, cold],
            "base_cold_damage_resistance_%",
            out var coldTotal));
        Assert.Equal(28m, coldTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, fire, chaos, cold],
            "base_lightning_damage_resistance_%",
            out var lightningTotal));
        Assert.Equal(13m, lightningTotal);
    }

    [Fact]
    public void TryCompute_DragonFingerShape_Fire33_Chaos17_Cold16()
    {
        var implicitPair = Component(
            "fire_and_cold_damage_resistance_%",
            "+16% to Fire and Cold Resistances",
            16m,
            ParsedModifierKind.Implicit);
        // Projected Fire/Chaos siblings share one physical craft key so Fire is not double-counted.
        var sharedPhysical = "phys:1:0:modifier:1:0";
        var fire = Component(
            "base_fire_damage_resistance_%",
            "+17% to Fire Resistance",
            17m,
            ParsedModifierKind.Suffix) with
        {
            PhysicalSourceIdentity = sharedPhysical + ":logic:base_fire_damage_resistance_%",
        };
        var chaos = Component(
            "base_chaos_damage_resistance_%",
            "+17% to Chaos Resistance",
            17m,
            ParsedModifierKind.Suffix) with
        {
            PhysicalSourceIdentity = sharedPhysical + ":logic:base_chaos_damage_resistance_%",
        };

        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, fire, chaos],
            "base_fire_damage_resistance_%",
            out var fireTotal));
        Assert.Equal(33m, fireTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, fire, chaos],
            "base_chaos_damage_resistance_%",
            out var chaosTotal));
        Assert.Equal(17m, chaosTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [implicitPair, fire, chaos],
            "base_cold_damage_resistance_%",
            out var coldTotal));
        Assert.Equal(16m, coldTotal);
    }

    [Fact]
    public void TryCompute_AggregatedDexPlusAllAttributes_Is42()
    {
        var sharedPhysical = "phys:0:0:modifier:0:0";
        var dex = Component(
            "additional_dexterity",
            "+30 to Dexterity",
            30m,
            ParsedModifierKind.Suffix) with
        {
            CanonicalSignature = "+<number> to Dexterity",
            Sources =
            [
                new SearchComponentSourceProvenance
                {
                    ComponentId = "pair-dex",
                    SourceModifierIndex = 0,
                    SourceLineIndex = 0,
                    SourceComponentIndex = 0,
                    OriginalText = "+22 to Dexterity",
                    CanonicalSignature = "+<number> to Dexterity",
                    ParsedKind = ParsedModifierKind.Suffix,
                    Locality = ModifierLocality.Global,
                    CanonicalNumericValues = [22m],
                    PhysicalSourceIdentity = sharedPhysical,
                },
                new SearchComponentSourceProvenance
                {
                    ComponentId = "dex-alone",
                    SourceModifierIndex = 2,
                    SourceLineIndex = 0,
                    SourceComponentIndex = 0,
                    OriginalText = "+8 to Dexterity",
                    CanonicalSignature = "+<number> to Dexterity",
                    ParsedKind = ParsedModifierKind.Suffix,
                    Locality = ModifierLocality.Global,
                    CanonicalNumericValues = [8m],
                },
            ],
        };
        var intelligence = Component(
            "additional_intelligence",
            "+22 to Intelligence",
            22m,
            ParsedModifierKind.Suffix) with
        {
            CanonicalSignature = "+<number> to Intelligence",
            PhysicalSourceIdentity = sharedPhysical + ":logic:additional_intelligence",
        };
        var allAttributes = Component(
            "additional_all_attributes",
            "+12 to all Attributes",
            12m,
            ParsedModifierKind.Suffix) with
        {
            CanonicalSignature = "+<number> to all Attributes",
            PhysicalSourceIdentity = "phys:1:0:modifier:1:0",
        };

        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [dex, intelligence, allAttributes],
            "additional_dexterity",
            out var dexTotal));
        Assert.Equal(42m, dexTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            [dex, intelligence, allAttributes],
            "additional_intelligence",
            out var intTotal));
        Assert.Equal(34m, intTotal);
    }

    private static ResolvedSearchComponent Component(
        string statId,
        string text,
        decimal value,
        ParsedModifierKind kind) =>
        new()
        {
            ComponentId = $"modifier:{statId}",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = text,
            CanonicalSignature = text.Contains('<') ? text : "+<number>% stub",
            ParsedKind = kind,
            GenerationType = kind == ParsedModifierKind.Implicit
                ? ModifierGenerationType.Implicit
                : ModifierGenerationType.Suffix,
            Locality = ModifierLocality.Global,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = $"id.{statId}",
            ResolvedStatIds = [statId],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            RequestedMinimum = value,
        };
}

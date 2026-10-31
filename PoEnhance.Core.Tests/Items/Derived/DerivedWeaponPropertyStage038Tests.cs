using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Tests.Items.Derived;

/// <summary>
/// Stage 03.8 focused coverage: genuine clipboard shapes from D:\Projects\zfortests_v2.txt
/// plus one clearly labeled synthetic chaos Rare (corpus has no N/M/R chaos weapon).
/// </summary>
public sealed class DerivedWeaponPropertyStage038Tests
{
    private readonly ItemTextParser parser = new();
    private readonly DerivedWeaponPropertyCalculator calculator = new();

    [Fact]
    public void EmpyreanGutter_RareMixedPhysElemental_MatchesAverageTimesAps()
    {
        // zfortests_v2.txt L1 — Empyrean Gutter / Engraved Greatsword
        var result = calculator.Calculate(parser.Parse("""
Item Class: Two Hand Swords
Rarity: Rare
Empyrean Gutter
Engraved Greatsword
--------
Two Handed Sword
Physical Damage: 49-102
Elemental Damage: 61-104 (augmented), 8-154 (augmented)
Critical Strike Chance: 5.00%
Attacks per Second: 1.30
Weapon Range: 1.3 metres
--------
Item Level: 84
--------
Adds 61 to 104 Fire Damage
Adds 8 to 154 Lightning Damage
"""));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(1.30m, result.AttacksPerSecond);
        Assert.Equal(5.00m, result.CriticalStrikeChance);
        Assert.Equal(75.5m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(98.15m, result.PhysicalDps);
        Assert.Equal(163.5m, result.ElementalDamage?.AverageHit);
        Assert.Equal(212.55m, result.ElementalDps);
        Assert.Null(result.ChaosDps);
        Assert.Equal(310.7m, result.TotalDps);
    }

    [Fact]
    public void ReaverAxe_NormalPhysicalOnly_DerivesPdpsAndTotal()
    {
        // zfortests_v2.txt L1159 — Normal Reaver Axe
        var result = calculator.Calculate(parser.Parse("""
Item Class: One Hand Axes
Rarity: Normal
Reaver Axe
--------
One Handed Axe
Physical Damage: 38-114
Critical Strike Chance: 5.00%
Attacks per Second: 1.20
Weapon Range: 1.1 metres
--------
Item Level: 83
"""));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(76m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(91.2m, result.PhysicalDps);
        Assert.Null(result.ElementalDps);
        Assert.Null(result.ChaosDps);
        Assert.Equal(91.2m, result.TotalDps);
        Assert.Equal(1.20m, result.AttacksPerSecond);
        Assert.Equal(5.00m, result.CriticalStrikeChance);
    }

    [Fact]
    public void GloomKnell_SpellColdDamageDoesNotInflateAttackDps()
    {
        // zfortests_v2.txt L343 — Gloom Knell / Stag Sceptre (spell cold is a modifier, not weapon property)
        var result = calculator.Calculate(parser.Parse("""
Item Class: Sceptres
Rarity: Rare
Gloom Knell
Stag Sceptre
--------
Sceptre
Quality: +11% (augmented)
Physical Damage: 43-80 (augmented)
Critical Strike Chance: 8.75% (augmented)
Attacks per Second: 1.30
Weapon Range: 1.1 metres
--------
Item Level: 84
--------
Adds 29(26-35) to 58(51-60) Cold Damage to Spells
25(25-29)% increased Critical Strike Chance
"""));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(61.5m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(79.95m, result.PhysicalDps);
        Assert.Null(result.ElementalDamage);
        Assert.Null(result.ElementalDps);
        Assert.Null(result.ChaosDps);
        Assert.Equal(79.95m, result.TotalDps);
        Assert.Equal(8.75m, result.CriticalStrikeChance);
    }

    [Fact]
    public void TempestSong_BowMixedDamage_UsesDisplayedAugmentedRanges()
    {
        // zfortests_v2.txt L218 — Tempest Song / Sniper Bow
        var result = calculator.Calculate(parser.Parse("""
Item Class: Bows
Rarity: Rare
Tempest Song
Sniper Bow
--------
Bow
Physical Damage: 36-108 (augmented)
Elemental Damage: 55-100 (augmented), 41-89 (augmented)
Critical Strike Chance: 6.70%
Attacks per Second: 1.25
--------
Item Level: 84
"""));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(72m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(90m, result.PhysicalDps);
        Assert.Equal(142.5m, result.ElementalDamage?.AverageHit);
        Assert.Equal(178.125m, result.ElementalDps);
        Assert.Equal(268.125m, result.TotalDps);
        Assert.Equal(1.25m, result.AttacksPerSecond);
        Assert.Equal(6.70m, result.CriticalStrikeChance);
    }

    [Fact]
    public void FoulbornRebuke_UniqueChaos_ContributesToTotalMatchingTradeDpsDefinition()
    {
        // zfortests_v2.txt L2697 — genuine Unique chaos clipboard (corpus lacks N/M/R chaos weapons).
        // Official Trade listing extended.dps = pdps + edps + chaos contribution (verified 2026-10-31).
        var result = calculator.Calculate(parser.Parse("""
Item Class: One Hand Swords
Rarity: Unique
Foulborn Rebuke of the Vaal
Vaal Blade
--------
One Handed Sword
Physical Damage: 114-206 (augmented)
Elemental Damage: 54-109 (augmented), 53-120 (augmented), 1-223 (augmented)
Chaos Damage: 64-135 (augmented)
Critical Strike Chance: 5.00%
Attacks per Second: 1.30
--------
Item Level: 83
--------
Adds 64 to 135 Chaos Damage
"""));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(160m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(208m, result.PhysicalDps);
        Assert.Equal(280m, result.ElementalDamage?.AverageHit);
        Assert.Equal(364m, result.ElementalDps);
        Assert.Equal(99.5m, result.ChaosDamage?.AverageHit);
        Assert.Equal(129.35m, result.ChaosDps);
        Assert.Equal(701.35m, result.TotalDps);
        Assert.Equal(
            result.PhysicalDps!.Value + result.ElementalDps!.Value + result.ChaosDps!.Value,
            result.TotalDps);
    }

    [Fact]
    public void SyntheticChaosRare_LabeledSynthetic_IncludesChaosInTotalOnly()
    {
        // SYNTHETIC clipboard — not present as Normal/Magic/Rare in zfortests_v2.txt.
        var result = calculator.Calculate(parser.Parse("""
Item Class: One Hand Swords
Rarity: Rare
Synthetic Chaos Edge
Corsair Sword
--------
One Handed Sword
Physical Damage: 20-40
Chaos Damage: 10-20 (augmented)
Critical Strike Chance: 5.00%
Attacks per Second: 1.50
--------
Item Level: 70
"""));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(30m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(45m, result.PhysicalDps);
        Assert.Null(result.ElementalDps);
        Assert.Equal(15m, result.ChaosDamage?.AverageHit);
        Assert.Equal(22.5m, result.ChaosDps);
        Assert.Equal(67.5m, result.TotalDps);
    }

    [Fact]
    public void ApexRapier_MagicPhysicalOnly_ParsesCritAndAps()
    {
        // zfortests_v2.txt L440 — Apex Rapier of Recuperation
        var result = calculator.Calculate(parser.Parse("""
Item Class: Thrusting One Hand Swords
Rarity: Magic
Apex Rapier of Recuperation
--------
Rapier
Physical Damage: 29-67
Critical Strike Chance: 5.70%
Attacks per Second: 1.40
Weapon Range: 1.4 metres
--------
Item Level: 66
"""));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(48m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(67.2m, result.PhysicalDps);
        Assert.Equal(67.2m, result.TotalDps);
        Assert.Equal(1.40m, result.AttacksPerSecond);
        Assert.Equal(5.70m, result.CriticalStrikeChance);
    }
}

using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class ItemPropertySemanticCatalogTests
{
    public static TheoryData<string[], string> ExactVectors => new()
    {
        { ["local_physical_damage_+%"], "weapon.physical-damage.increased-percent.local" },
        { ["local_minimum_added_physical_damage", "local_maximum_added_physical_damage"], "weapon.physical-damage.added.local" },
        { ["local_minimum_added_fire_damage", "local_maximum_added_fire_damage"], "weapon.fire-damage.added.local" },
        { ["local_minimum_added_cold_damage", "local_maximum_added_cold_damage"], "weapon.cold-damage.added.local" },
        { ["local_minimum_added_lightning_damage", "local_maximum_added_lightning_damage"], "weapon.lightning-damage.added.local" },
        { ["local_minimum_added_chaos_damage", "local_maximum_added_chaos_damage"], "weapon.chaos-damage.added.local" },
        { ["local_attack_speed_+%"], "weapon.attack-speed.increased-percent.local" },
        { ["local_critical_strike_chance_+%"], "weapon.critical-strike-chance.increased-percent.local" },
        { ["local_critical_strike_chance"], "weapon.critical-strike-chance.added.local" },
        { ["local_item_quality_+"], "item.quality.added.local" },
        { ["local_base_physical_damage_reduction_rating"], "item.armour.added.local" },
        { ["local_physical_damage_reduction_rating_+%"], "item.armour.increased-percent.local" },
        { ["local_base_evasion_rating"], "item.evasion.added.local" },
        { ["local_evasion_rating_+%"], "item.evasion.increased-percent.local" },
        { ["local_energy_shield"], "item.energy-shield.added.local" },
        { ["local_energy_shield_+%"], "item.energy-shield.increased-percent.local" },
        { ["local_ward"], "item.ward.added.local" },
        { ["local_ward_+%"], "item.ward.increased-percent.local" },
        { ["local_additional_block_chance_%"], "item.block.added.local" },
        { ["local_block_chance_+%"], "item.block.increased-percent.local" },
        { ["local_armour_and_evasion_+%"], "item.armour-evasion.increased-percent.local" },
        { ["local_armour_and_energy_shield_+%"], "item.armour-energy-shield.increased-percent.local" },
        { ["local_evasion_and_energy_shield_+%"], "item.evasion-energy-shield.increased-percent.local" },
        { ["local_armour_and_evasion_and_energy_shield_+%"], "item.armour-evasion-energy-shield.increased-percent.local" },
        { ["local_evasion_rating_and_energy_shield"], "item.evasion-energy-shield.added.local" },
    };

    public static TheoryData<string[]> UnsupportedVectors => new()
    {
        { ["local_elemental_damage_+%"] },
        { ["local_explicit_mod_effect_+%"] },
        { ["local_explicit_elemental_damage_mod_effect_+%"] },
        { ["local_explicit_physical_and_chaos_damage_mod_effect_+%"] },
        { ["local_weapon_no_physical_damage"] },
        { ["local_no_energy_shield"] },
        { ["local_no_block_chance"] },
        { ["local_maximum_quality_+"] },
        { ["local_maximum_quality_is_%"] },
        { ["local_is_max_quality"] },
        { ["local_quality_does_not_increase_physical_damage"] },
        { ["local_quality_does_not_increase_defences"] },
        { ["attack_speed_+%"] },
        { ["critical_strike_chance_+%"] },
        { ["spell_critical_strike_chance_+%"] },
        { ["local_attack_speed_+%_if_condition"] },
        { ["local_critical_strike_chance_+%_if_item_corrupted"] },
        { ["local_critical_strike_chance_+%_if_condition"] },
        { ["local_attacks_with_this_weapon_physical_damage_+%_per_250_evasion"] },
        { ["local_minimum_added_physical_damage_vs_ignited_enemies", "local_maximum_added_physical_damage_vs_ignited_enemies"] },
        { ["local_quality_does_not_increase_physical_damage", "local_attack_speed_+%_per_8%_quality"] },
        { ["local_quality_does_not_increase_physical_damage", "local_critical_strike_chance_+%_per_4%_quality"] },
        { ["local_quality_does_not_increase_physical_damage", "local_elemental_damage_+%_per_2%_quality"] },
        { ["local_critical_strike_chance_+%_per_4%_quality", "local_attack_speed_+%_per_8%_quality"] },
        { ["base_stun_recovery_+%"] },
        { ["flask_armour_+%"] },
        { ["aura_attack_speed_+%"] },
        { ["minion_attack_speed_+%"] },
        { ["local_socketed_gems_attack_speed_+%"] },
    };

    [Theory]
    [MemberData(nameof(ExactVectors))]
    public void FindItemPropertySemanticByOrderedStatVector_ExactVectorResolves(
        string[] vector,
        string expectedId)
    {
        var catalog = GameDataCatalog.FromPackage(ItemPropertySemanticTestFixtures.CreatePackage());

        var descriptor = catalog.FindItemPropertySemanticByOrderedStatVector(vector);

        Assert.NotNull(descriptor);
        Assert.Equal(expectedId, descriptor.Id);
        Assert.Equal(vector, descriptor.OrderedStatIds);
    }

    [Fact]
    public void FindItemPropertySemanticByOrderedStatVector_NonExactVectorsDoNotResolve()
    {
        var catalog = GameDataCatalog.FromPackage(ItemPropertySemanticTestFixtures.CreatePackage());

        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(
            ["local_maximum_added_physical_damage", "local_minimum_added_physical_damage"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(
            ["local_minimum_added_physical_damage"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(
            ["local_minimum_added_physical_damage", "local_maximum_added_physical_damage", "extra_stat"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(["unknown_stat"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(
            ["spell_minimum_added_fire_damage", "spell_maximum_added_fire_damage"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(
            ["attack_minimum_added_physical_damage", "attack_maximum_added_physical_damage"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(
            ["local_minimum_added_fire_damage_vs_bleeding_enemies", "local_maximum_added_fire_damage_vs_bleeding_enemies"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(["attack_speed_+%"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(["critical_strike_chance_+%"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(
            ["local_quality_does_not_increase_physical_damage", "local_attack_speed_+%_per_8%_quality"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(
            ["local_quality_does_not_increase_physical_damage", "local_critical_strike_chance_+%_per_4%_quality"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(["local_explicit_mod_effect_+%"]));
        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(["local_no_block_chance"]));
    }

    [Theory]
    [MemberData(nameof(UnsupportedVectors))]
    public void FindItemPropertySemanticByOrderedStatVector_UnsupportedFamiliesRemainUnreviewed(
        string[] vector)
    {
        var catalog = GameDataCatalog.FromPackage(ItemPropertySemanticTestFixtures.CreatePackage());

        Assert.Null(catalog.FindItemPropertySemanticByOrderedStatVector(vector));
    }
}

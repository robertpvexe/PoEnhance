using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

internal static class ItemPropertySemanticTestFixtures
{
    public const string ReviewVersion = "aps-crit-defence-v1";

    public static readonly IReadOnlyList<string> IncreasedPhysicalVector =
        ["local_physical_damage_+%"];

    public static readonly IReadOnlyList<string> AddedPhysicalVector =
        ["local_minimum_added_physical_damage", "local_maximum_added_physical_damage"];

    public static readonly IReadOnlyList<string> AddedFireVector =
        ["local_minimum_added_fire_damage", "local_maximum_added_fire_damage"];

    public static readonly IReadOnlyList<string> AddedColdVector =
        ["local_minimum_added_cold_damage", "local_maximum_added_cold_damage"];

    public static readonly IReadOnlyList<string> AddedLightningVector =
        ["local_minimum_added_lightning_damage", "local_maximum_added_lightning_damage"];

    public static readonly IReadOnlyList<string> AddedChaosVector =
        ["local_minimum_added_chaos_damage", "local_maximum_added_chaos_damage"];

    public static readonly IReadOnlyList<string> IncreasedAttackSpeedVector =
        ["local_attack_speed_+%"];

    public static readonly IReadOnlyList<string> IncreasedCriticalStrikeChanceVector =
        ["local_critical_strike_chance_+%"];

    public static readonly IReadOnlyList<string> AddedCriticalStrikeChanceVector =
        ["local_critical_strike_chance"];

    public static readonly IReadOnlyList<string> AddedQualityVector =
        ["local_item_quality_+"];

    public static readonly IReadOnlyList<string> AddedArmourVector =
        ["local_base_physical_damage_reduction_rating"];

    public static readonly IReadOnlyList<string> IncreasedArmourVector =
        ["local_physical_damage_reduction_rating_+%"];

    public static readonly IReadOnlyList<string> AddedEvasionVector =
        ["local_base_evasion_rating"];

    public static readonly IReadOnlyList<string> IncreasedEvasionVector =
        ["local_evasion_rating_+%"];

    public static readonly IReadOnlyList<string> AddedEnergyShieldVector =
        ["local_energy_shield"];

    public static readonly IReadOnlyList<string> IncreasedEnergyShieldVector =
        ["local_energy_shield_+%"];

    public static readonly IReadOnlyList<string> AddedWardVector = ["local_ward"];

    public static readonly IReadOnlyList<string> IncreasedWardVector = ["local_ward_+%"];

    public static readonly IReadOnlyList<string> AddedBlockVector =
        ["local_additional_block_chance_%"];

    public static readonly IReadOnlyList<string> IncreasedBlockVector =
        ["local_block_chance_+%"];

    public static readonly IReadOnlyList<string> IncreasedArmourEvasionVector =
        ["local_armour_and_evasion_+%"];

    public static readonly IReadOnlyList<string> IncreasedArmourEnergyShieldVector =
        ["local_armour_and_energy_shield_+%"];

    public static readonly IReadOnlyList<string> IncreasedEvasionEnergyShieldVector =
        ["local_evasion_and_energy_shield_+%"];

    public static readonly IReadOnlyList<string> IncreasedArmourEvasionEnergyShieldVector =
        ["local_armour_and_evasion_and_energy_shield_+%"];

    public static readonly IReadOnlyList<string> AddedEvasionEnergyShieldVector =
        ["local_evasion_rating_and_energy_shield"];

    public static IReadOnlyList<IReadOnlyList<string>> ReviewedVectors =>
    [
        IncreasedPhysicalVector,
        AddedPhysicalVector,
        AddedFireVector,
        AddedColdVector,
        AddedLightningVector,
        AddedChaosVector,
        IncreasedAttackSpeedVector,
        IncreasedCriticalStrikeChanceVector,
        AddedCriticalStrikeChanceVector,
        AddedQualityVector,
        AddedArmourVector,
        IncreasedArmourVector,
        AddedEvasionVector,
        IncreasedEvasionVector,
        AddedEnergyShieldVector,
        IncreasedEnergyShieldVector,
        AddedWardVector,
        IncreasedWardVector,
        AddedBlockVector,
        IncreasedBlockVector,
        IncreasedArmourEvasionVector,
        IncreasedArmourEnergyShieldVector,
        IncreasedEvasionEnergyShieldVector,
        IncreasedArmourEvasionEnergyShieldVector,
        AddedEvasionEnergyShieldVector,
    ];

    public static GameDataPackage CreatePackage()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var semanticStats = ReviewedVectors
            .SelectMany(vector => vector)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(statId => new StatDefinition
            {
                Id = statId,
                IsLocal = true,
            });

        return package with
        {
            Stats = package.Stats.Concat(semanticStats).ToArray(),
            ItemPropertySemantics =
            [
                CreateDescriptor(
                    "weapon.physical-damage.increased-percent.local",
                    IncreasedPhysicalVector,
                    ItemPropertyTarget.PhysicalDamage,
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "weapon.physical-damage.added.local",
                    AddedPhysicalVector,
                    ItemPropertyTarget.PhysicalDamage,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "weapon.fire-damage.added.local",
                    AddedFireVector,
                    ItemPropertyTarget.FireDamage,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "weapon.cold-damage.added.local",
                    AddedColdVector,
                    ItemPropertyTarget.ColdDamage,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "weapon.lightning-damage.added.local",
                    AddedLightningVector,
                    ItemPropertyTarget.LightningDamage,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "weapon.chaos-damage.added.local",
                    AddedChaosVector,
                    ItemPropertyTarget.ChaosDamage,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "weapon.attack-speed.increased-percent.local",
                    IncreasedAttackSpeedVector,
                    ItemPropertyTarget.AttacksPerSecond,
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "weapon.critical-strike-chance.increased-percent.local",
                    IncreasedCriticalStrikeChanceVector,
                    ItemPropertyTarget.CriticalStrikeChance,
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "weapon.critical-strike-chance.added.local",
                    AddedCriticalStrikeChanceVector,
                    ItemPropertyTarget.CriticalStrikeChance,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "item.quality.added.local",
                    AddedQualityVector,
                    ItemPropertyTarget.Quality,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "item.armour.added.local",
                    AddedArmourVector,
                    ItemPropertyTarget.Armour,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "item.armour.increased-percent.local",
                    IncreasedArmourVector,
                    ItemPropertyTarget.Armour,
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.evasion.added.local",
                    AddedEvasionVector,
                    ItemPropertyTarget.Evasion,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "item.evasion.increased-percent.local",
                    IncreasedEvasionVector,
                    ItemPropertyTarget.Evasion,
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.energy-shield.added.local",
                    AddedEnergyShieldVector,
                    ItemPropertyTarget.EnergyShield,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "item.energy-shield.increased-percent.local",
                    IncreasedEnergyShieldVector,
                    ItemPropertyTarget.EnergyShield,
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.ward.added.local",
                    AddedWardVector,
                    ItemPropertyTarget.Ward,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "item.ward.increased-percent.local",
                    IncreasedWardVector,
                    ItemPropertyTarget.Ward,
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.block.added.local",
                    AddedBlockVector,
                    ItemPropertyTarget.Block,
                    ItemPropertyOperation.Added),
                CreateDescriptor(
                    "item.block.increased-percent.local",
                    IncreasedBlockVector,
                    ItemPropertyTarget.Block,
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.armour-evasion.increased-percent.local",
                    IncreasedArmourEvasionVector,
                    [ItemPropertyTarget.Armour, ItemPropertyTarget.Evasion],
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.armour-energy-shield.increased-percent.local",
                    IncreasedArmourEnergyShieldVector,
                    [ItemPropertyTarget.Armour, ItemPropertyTarget.EnergyShield],
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.evasion-energy-shield.increased-percent.local",
                    IncreasedEvasionEnergyShieldVector,
                    [ItemPropertyTarget.Evasion, ItemPropertyTarget.EnergyShield],
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.armour-evasion-energy-shield.increased-percent.local",
                    IncreasedArmourEvasionEnergyShieldVector,
                    [ItemPropertyTarget.Armour, ItemPropertyTarget.Evasion, ItemPropertyTarget.EnergyShield],
                    ItemPropertyOperation.IncreasedPercent),
                CreateDescriptor(
                    "item.evasion-energy-shield.added.local",
                    AddedEvasionEnergyShieldVector,
                    [ItemPropertyTarget.Evasion, ItemPropertyTarget.EnergyShield],
                    ItemPropertyOperation.Added),
            ],
        };
    }

    public static ItemPropertySemanticDescriptor CreateDescriptor(
        string id,
        IReadOnlyList<string> orderedStatIds,
        ItemPropertyTarget target,
        ItemPropertyOperation operation)
    {
        return CreateDescriptor(id, orderedStatIds, [target], operation);
    }

    public static ItemPropertySemanticDescriptor CreateDescriptor(
        string id,
        IReadOnlyList<string> orderedStatIds,
        IReadOnlyList<ItemPropertyTarget> targets,
        ItemPropertyOperation operation)
    {
        return new ItemPropertySemanticDescriptor
        {
            Id = id,
            OrderedStatIds = orderedStatIds,
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Targets = targets,
                    Operation = operation,
                },
            ],
            Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
            Evidence = [CreateEvidence()],
        };
    }

    public static ItemPropertySemanticEvidence CreateEvidence()
    {
        return new ItemPropertySemanticEvidence
        {
            Method = ItemPropertySemanticEvidenceMethod.ReviewedOverride,
            SourceId = "poenhance.item-property-semantics",
            ReviewVersion = ReviewVersion,
            ReviewReference = "complete-item-property-contributor-and-locality-audit:2026-07-17",
            CompatibleSourceId = "repoe",
            CompatibleSourceVersion = "c50acab2ed660a70511e7f91ee09db4e632089e4",
        };
    }
}

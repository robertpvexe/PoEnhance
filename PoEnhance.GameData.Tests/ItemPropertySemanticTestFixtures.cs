using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

internal static class ItemPropertySemanticTestFixtures
{
    public const string ReviewVersion = "weapon-dps-v1";

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

    public static IReadOnlyList<IReadOnlyList<string>> InitialVectors =>
    [
        IncreasedPhysicalVector,
        AddedPhysicalVector,
        AddedFireVector,
        AddedColdVector,
        AddedLightningVector,
        AddedChaosVector,
    ];

    public static GameDataPackage CreatePackage()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var semanticStats = InitialVectors
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
            ],
        };
    }

    public static ItemPropertySemanticDescriptor CreateDescriptor(
        string id,
        IReadOnlyList<string> orderedStatIds,
        ItemPropertyTarget target,
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
                    Targets = [target],
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
            ReviewReference = "repoe-item-property-semantic-source-audit:2026-07-16#initial-weapon-vectors",
            CompatibleSourceId = "repoe",
            CompatibleSourceVersion = "c50acab2ed660a70511e7f91ee09db4e632089e4",
        };
    }
}

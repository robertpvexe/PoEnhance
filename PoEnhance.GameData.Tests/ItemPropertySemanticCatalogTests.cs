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
    }
}

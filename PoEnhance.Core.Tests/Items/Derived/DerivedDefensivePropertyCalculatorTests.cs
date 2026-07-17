using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.Derived;

public sealed class DerivedDefensivePropertyCalculatorTests
{
    private readonly ItemTextParser parser = new();
    private readonly DerivedWeaponPropertyCalculator calculator = new();

    [Theory]
    [InlineData(0, 100)]
    [InlineData(10, 100)]
    [InlineData(20, 100)]
    [InlineData(28, 100)]
    public void OrdinaryQuality_NormalizesExactArmourRollToQ20(int observedQuality, int baseRoll)
    {
        var displayed = Round(baseRoll * (1m + observedQuality / 100m));
        var item = parser.Parse(Item("Armour", displayed, observedQuality));

        var result = calculator.CalculateDefensiveQ20(item, Base(armour: (baseRoll, baseRoll)), []);

        var armour = Assert.Single(result.Properties);
        Assert.Equal(ItemPropertyTarget.Armour, armour.Target);
        Assert.Equal(120m, armour.Value);
        Assert.True(armour.IsQ20);
        Assert.Equal(baseRoll, armour.ReconstructedBaseValue);
        Assert.Null(armour.UnsupportedReason);
    }

    [Fact]
    public void SlinkBoots_ReconstructsQ10RollAndNormalizesToQ20()
    {
        var item = parser.Parse(Item("Evasion Rating", 445, quality: 10));
        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(evasion: (246, 283)),
            [Effect(ItemPropertyTarget.Evasion, ItemPropertyOperation.Added, 42),
             Effect(ItemPropertyTarget.Evasion, ItemPropertyOperation.IncreasedPercent, 34)]);

        var evasion = Assert.Single(result.Properties);
        Assert.Equal(486m, evasion.Value);
        Assert.Equal(260, evasion.ReconstructedBaseValue);
        Assert.True(evasion.IsQ20);
    }

    [Fact]
    public void SkullRoad_Q20EnergyShieldExcludesStunAndBlockRecovery()
    {
        var item = parser.Parse(Item("Energy Shield", 57, quality: 20));
        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(energyShield: (36, 42)),
            [Effect(ItemPropertyTarget.EnergyShield, ItemPropertyOperation.IncreasedPercent, 25),
             new DerivedWeaponModifierEffect
             {
                 ComponentId = "stun-recovery",
                 IsExactlyResolved = true,
                 IsLocal = false,
                 HasProvenStatAssociation = true,
                 ResolvedStatIds = ["base_stun_recovery_+%"],
                 CanonicalNumericValues = [10m],
             }]);

        var energyShield = Assert.Single(result.Properties);
        Assert.Equal(57m, energyShield.Value);
        Assert.Equal(38, energyShield.ReconstructedBaseValue);
        Assert.True(energyShield.IsQ20);
        Assert.Single(energyShield.ModifierContributions);
    }

    [Fact]
    public void GaleWrap_HybridEffectsProduceSeparateQ20Parents()
    {
        var item = parser.Parse("""
            Item Class: Body Armours
            Rarity: Rare
            Gale Wrap
            Marshall's Brigandine
            --------
            Quality: +20% (augmented)
            Armour: 2330 (augmented)
            Evasion Rating: 2349 (augmented)
            --------
            Item Level: 84
            """);
        var effects = new[]
        {
            Effect(ItemPropertyTarget.Armour, ItemPropertyOperation.Added, 226),
            Effect(ItemPropertyTarget.Evasion, ItemPropertyOperation.Added, 234),
            HybridEffect(ItemPropertyOperation.IncreasedPercent, 37,
                ItemPropertyTarget.Armour, ItemPropertyTarget.Evasion),
            HybridEffect(ItemPropertyOperation.IncreasedPercent, 68,
                ItemPropertyTarget.Armour, ItemPropertyTarget.Evasion),
        };

        var result = calculator.CalculateDefensiveQ20(item, Base(armour: (627, 721), evasion: (627, 721)), effects);

        Assert.Collection(result.Properties,
            armour =>
            {
                Assert.Equal(ItemPropertyTarget.Armour, armour.Target);
                Assert.Equal(2330m, armour.Value);
                Assert.Equal(721, armour.ReconstructedBaseValue);
            },
            evasion =>
            {
                Assert.Equal(ItemPropertyTarget.Evasion, evasion.Target);
                Assert.Equal(2349m, evasion.Value);
                Assert.Equal(721, evasion.ReconstructedBaseValue);
            });
    }

    [Fact]
    public void MiracleBastion_NormalizesEnergyShieldAndEvasionAndPreservesBaseBlock()
    {
        var item = parser.Parse("""
            Item Class: Shields
            Rarity: Rare
            Miracle Bastion
            Supreme Spiked Shield
            --------
            Chance to Block: 24%
            Evasion Rating: 429 (augmented)
            Energy Shield: 124 (augmented)
            --------
            Item Level: 84
            """);
        var effects = new[]
        {
            Effect(ItemPropertyTarget.EnergyShield, ItemPropertyOperation.Added, 15),
            HybridEffect(ItemPropertyOperation.IncreasedPercent, 28,
                ItemPropertyTarget.Evasion, ItemPropertyTarget.EnergyShield),
            Effect(ItemPropertyTarget.Evasion, ItemPropertyOperation.Added, 59),
            Effect(ItemPropertyTarget.EnergyShield, ItemPropertyOperation.Added, 26),
        };

        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(evasion: (242, 278), energyShield: (49, 57), block: 24),
            effects);

        Assert.Collection(result.Properties,
            energyShield =>
            {
                Assert.Equal(ItemPropertyTarget.EnergyShield, energyShield.Target);
                Assert.Equal(149m, energyShield.Value);
                Assert.True(energyShield.IsQ20);
            },
            evasion =>
            {
                Assert.Equal(ItemPropertyTarget.Evasion, evasion.Target);
                Assert.Equal(515m, evasion.Value);
                Assert.True(evasion.IsQ20);
            },
            block =>
            {
                Assert.Equal(ItemPropertyTarget.Block, block.Target);
                Assert.Equal(24m, block.Value);
                Assert.False(block.IsQ20);
                Assert.Null(block.UnsupportedReason);
            });
    }

    [Fact]
    public async Task Ward_RemainsExactDisplayedValueWithoutUnprovenQ20Label()
    {
        var item = parser.Parse(Item("Ward", 112, quality: 20));
        var loaded = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(loaded.IsSuccess);
        var runicCrest = Assert.Single(loaded.Package!.ItemBases, itemBase =>
            itemBase.Id == "Metadata/Items/Armours/Helmets/HelmetExpedition2");

        var result = calculator.CalculateDefensiveQ20(item, runicCrest, []);

        var ward = Assert.Single(result.Properties);
        Assert.Equal(ItemPropertyTarget.Ward, ward.Target);
        Assert.Equal(112m, ward.Value);
        Assert.False(ward.IsQ20);
    }

    [Fact]
    public void MissingBaseAndAlternateQualityControl_AreExplicitlyUnsupported()
    {
        var item = parser.Parse(Item("Energy Shield", 100, quality: 28));
        var control = Effect(ItemPropertyTarget.EnergyShield, ItemPropertyOperation.IncreasedPercent, 0) with
        {
            ResolvedStatIds = ["local_quality_does_not_increase_defences"],
            ReviewedItemPropertySemantic = null,
        };

        var result = calculator.CalculateDefensiveQ20(item, itemBase: null, [control]);

        var property = Assert.Single(result.Properties);
        Assert.False(property.IsQ20);
        Assert.NotNull(property.UnsupportedReason);
        Assert.Equal(100m, property.Value);
    }

    private static DerivedWeaponModifierEffect Effect(
        ItemPropertyTarget target,
        ItemPropertyOperation operation,
        decimal value) => HybridEffect(operation, value, target);

    private static DerivedWeaponModifierEffect HybridEffect(
        ItemPropertyOperation operation,
        decimal value,
        params ItemPropertyTarget[] targets) => new()
        {
            ComponentId = Guid.NewGuid().ToString("N"),
            SourceModifierIndex = 0,
            ResolvedModifierId = "test.mod",
            IsExactlyResolved = true,
            IsLocal = true,
            HasProvenStatAssociation = true,
            ResolvedStatIds = ["local_test"],
            CanonicalNumericValues = [value],
            ReviewedItemPropertySemantic = new ItemPropertySemanticDescriptor
            {
                Id = "test.semantic",
                Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
                Contributions = [new ItemPropertyContribution { Targets = targets, Operation = operation }],
            },
        };

    private static ItemBaseRecord Base(
        (int Min, int Max)? armour = null,
        (int Min, int Max)? evasion = null,
        (int Min, int Max)? energyShield = null,
        (int Min, int Max)? ward = null,
        int? block = null) => new()
        {
            Id = "base",
            DefenceProperties = new ItemBaseDefenceProperties
            {
                ArmourMinimum = armour?.Min,
                ArmourMaximum = armour?.Max,
                EvasionRatingMinimum = evasion?.Min,
                EvasionRatingMaximum = evasion?.Max,
                EnergyShieldMinimum = energyShield?.Min,
                EnergyShieldMaximum = energyShield?.Max,
                WardMinimum = ward?.Min,
                WardMaximum = ward?.Max,
                ChanceToBlockPercent = block,
                Sources = [new GameDataSourceReference { SourceId = "repoe", ExternalId = "base" }],
            },
        };

    private static string Item(string property, decimal value, int quality) => $$"""
        Item Class: Body Armours
        Rarity: Normal
        Test Item
        Test Base
        --------
        Quality: +{{quality}}%
        {{property}}: {{value}}
        --------
        Item Level: 85
        """;

    private static decimal Round(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(relativeParts)}.");
    }
}

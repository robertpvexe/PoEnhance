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
    public void Q20DisplayedArmour_IsNotQualityScaledTwice()
    {
        var item = parser.Parse(Item("Armour", 120, quality: 20));

        var result = calculator.CalculateDefensiveQ20(item, Base(armour: (100, 100)), []);

        var armour = Assert.Single(result.Properties);
        Assert.Equal(120m, armour.Value);
        Assert.True(armour.IsQ20);
        Assert.Equal(100, armour.ReconstructedBaseValue);
    }

    [Fact]
    public void IncompleteHybridReconstruction_FallsBackToCurrentDisplayedValuesWithoutQ20Claim()
    {
        var item = parser.Parse("""
            Item Class: Body Armours
            Rarity: Rare
            Hybrid Fallback
            Test Base
            --------
            Quality: +20% (augmented)
            Armour: 420 (augmented)
            Evasion Rating: 420 (augmented)
            --------
            Item Level: 85
            """);

        var result = calculator.CalculateDefensiveQ20(item, itemBase: null, []);

        Assert.Collection(result.Properties,
            armour =>
            {
                Assert.Equal(ItemPropertyTarget.Armour, armour.Target);
                Assert.Equal(420m, armour.Value);
                Assert.False(armour.IsQ20);
                Assert.NotNull(armour.UnsupportedReason);
            },
            evasion =>
            {
                Assert.Equal(ItemPropertyTarget.Evasion, evasion.Target);
                Assert.Equal(420m, evasion.Value);
                Assert.False(evasion.IsQ20);
                Assert.NotNull(evasion.UnsupportedReason);
            });
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
    public void ArmourEnergyShieldHybrid_ProducesSeparateQ20Parents()
    {
        // Synthetic AR+ES hybrid: same mechanism as Gale Wrap AR+EV, not a hard-coded base case.
        var item = parser.Parse("""
            Item Class: Body Armours
            Rarity: Rare
            Synthetic Aes Wrap
            Saintly Chainmail
            --------
            Quality: +20% (augmented)
            Armour: 1283 (augmented)
            Energy Shield: 306 (augmented)
            --------
            Item Level: 78
            """);
        var effects = new[]
        {
            HybridEffect(ItemPropertyOperation.IncreasedPercent, 91,
                ItemPropertyTarget.Armour, ItemPropertyTarget.EnergyShield),
            HybridEffect(ItemPropertyOperation.IncreasedPercent, 41,
                ItemPropertyTarget.Armour, ItemPropertyTarget.EnergyShield),
        };

        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(armour: (401, 461), energyShield: (96, 110)),
            effects);

        Assert.Collection(result.Properties,
            energyShield =>
            {
                Assert.Equal(ItemPropertyTarget.EnergyShield, energyShield.Target);
                Assert.Equal(306m, energyShield.Value);
                Assert.True(energyShield.IsQ20);
                Assert.Equal(110, energyShield.ReconstructedBaseValue);
                Assert.Equal(132m, energyShield.LocalIncreasedPercent);
            },
            armour =>
            {
                Assert.Equal(ItemPropertyTarget.Armour, armour.Target);
                Assert.Equal(1283m, armour.Value);
                Assert.True(armour.IsQ20);
                Assert.Equal(461, armour.ReconstructedBaseValue);
                Assert.Equal(132m, armour.LocalIncreasedPercent);
            });
        Assert.DoesNotContain(result.Properties, property => property.Target == ItemPropertyTarget.Block);
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
    public void GoldenBuckler_BlockFinalValueFloorsAtThePositiveHalfBoundary()
    {
        const decimal baseBlock = 25m;
        const decimal increasedBlock = 62m;
        var raw = baseBlock * (1m + increasedBlock / 100m);

        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(BlockItem(40m)),
            Base(block: 25),
            [Effect(ItemPropertyTarget.Block, ItemPropertyOperation.IncreasedPercent, increasedBlock)]);

        var block = Assert.Single(result.Properties);
        Assert.Equal(40.5m, raw);
        Assert.Equal(40m, block.Value);
        Assert.Equal(25, block.ReconstructedBaseValue);
        Assert.Equal(0m, block.LocalAdded);
        Assert.Equal(62m, block.LocalIncreasedPercent);
        Assert.False(block.IsQ20);
        Assert.Null(block.UnsupportedReason);
    }

    [Fact]
    public void BlockFinalValueFloorsTheExistingFortyFivePercentCase()
    {
        const decimal baseBlock = 25m;
        const decimal increasedBlock = 45m;
        var raw = baseBlock * (1m + increasedBlock / 100m);

        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(BlockItem(36m)),
            Base(block: 25),
            [Effect(ItemPropertyTarget.Block, ItemPropertyOperation.IncreasedPercent, increasedBlock)]);

        var block = Assert.Single(result.Properties);
        Assert.Equal(36.25m, raw);
        Assert.Equal(36m, block.Value);
        Assert.Null(block.UnsupportedReason);
    }

    [Fact]
    public void BlockFinalValueFloorsBeyondTheHalfBoundaryWithoutAHalfSpecificRule()
    {
        const decimal baseBlock = 25m;
        const decimal increasedBlock = 65m;
        var raw = baseBlock * (1m + increasedBlock / 100m);

        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(BlockItem(41m)),
            Base(block: 25),
            [Effect(ItemPropertyTarget.Block, ItemPropertyOperation.IncreasedPercent, increasedBlock)]);

        var block = Assert.Single(result.Properties);
        Assert.Equal(41.25m, raw);
        Assert.Equal(41m, block.Value);
        Assert.Null(block.UnsupportedReason);
    }

    [Fact]
    public void BlockFinalValueAppliesLocalAddedBeforeLocalIncreased()
    {
        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(BlockItem(40m)),
            Base(block: 25),
            [Effect(ItemPropertyTarget.Block, ItemPropertyOperation.Added, 3m),
             Effect(ItemPropertyTarget.Block, ItemPropertyOperation.IncreasedPercent, 45m)]);

        var block = Assert.Single(result.Properties);
        Assert.Equal(40.6m, (25m + 3m) * 1.45m);
        Assert.Equal(40m, block.Value);
        Assert.Equal(3m, block.LocalAdded);
        Assert.Equal(45m, block.LocalIncreasedPercent);
        Assert.Null(block.UnsupportedReason);
    }

    [Fact]
    public void BlockIgnoresSpellRecoveryGlobalAndConditionalBlockEffects()
    {
        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(BlockItem(36m)),
            Base(block: 25),
            [Effect(ItemPropertyTarget.Block, ItemPropertyOperation.IncreasedPercent, 45m),
             UnrelatedEffect("base_spell_block_chance_+%"),
             UnrelatedEffect("base_block_recovery_+%"),
             UnrelatedEffect("base_stun_recovery_+%"),
             UnrelatedEffect("base_block_chance_+%"),
             UnrelatedEffect("base_block_chance_+%_while_on_low_life")]);

        var block = Assert.Single(result.Properties);
        Assert.Equal(36m, block.Value);
        Assert.Equal(45m, block.LocalIncreasedPercent);
        Assert.Single(block.ModifierContributions);
        Assert.Null(block.UnsupportedReason);
    }

    [Fact]
    public void Ward_QualityNormalizedLikeOtherLocalDefences_FromQ0Clipboard()
    {
        // Synthetic: mirrors Hypnotic Keep math (base 180, +14, 65% → clipboard 320 @ Q0 → Q20 384).
        var item = parser.Parse("""
            Item Class: Helmets
            Rarity: Rare
            Synthetic Ward Helm
            Runic Crown
            --------
            Ward: 320 (augmented)
            --------
            Item Level: 70
            """);
        var effects = new[]
        {
            Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.Added, 14),
            Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.IncreasedPercent, 65),
        };

        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(ward: (164, 189)),
            effects);

        var ward = Assert.Single(result.Properties);
        Assert.Equal(ItemPropertyTarget.Ward, ward.Target);
        Assert.Equal(384m, ward.Value);
        Assert.True(ward.IsQ20);
        Assert.Equal(180, ward.ReconstructedBaseValue);
        Assert.Equal(0m, ward.ObservedQuality);
    }

    [Fact]
    public void Ward_AlreadyAtQ20_IsNotQualityScaledTwice()
    {
        // Synthetic: same reconstructed locals as above, clipboard already shows Q20 total.
        var item = parser.Parse("""
            Item Class: Helmets
            Rarity: Rare
            Synthetic Ward Helm Q20
            Runic Crown
            --------
            Quality: +20% (augmented)
            Ward: 384 (augmented)
            --------
            Item Level: 70
            """);
        var effects = new[]
        {
            Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.Added, 14),
            Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.IncreasedPercent, 65),
        };

        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(ward: (164, 189)),
            effects);

        var ward = Assert.Single(result.Properties);
        Assert.Equal(384m, ward.Value);
        Assert.True(ward.IsQ20);
        Assert.Equal(180, ward.ReconstructedBaseValue);
    }

    [Fact]
    public void Ward_IntermediateQuality_NormalizesFromCurrentQualityNotBlindMultiply()
    {
        // Synthetic: Q10 clipboard Round((180+14)*1.65*1.1)=Round(352.11)=352 → Q20 384.
        var item = parser.Parse("""
            Item Class: Helmets
            Rarity: Rare
            Synthetic Ward Helm Q10
            Runic Crown
            --------
            Quality: +10% (augmented)
            Ward: 352 (augmented)
            --------
            Item Level: 70
            """);
        var effects = new[]
        {
            Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.Added, 14),
            Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.IncreasedPercent, 65),
        };

        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(ward: (164, 189)),
            effects);

        var ward = Assert.Single(result.Properties);
        Assert.Equal(384m, ward.Value);
        Assert.True(ward.IsQ20);
        Assert.Equal(180, ward.ReconstructedBaseValue);
        Assert.Equal(10m, ward.ObservedQuality);
    }

    [Fact]
    public void Ward_RoundingUsesAwayFromZeroLikeOtherDefences()
    {
        // Synthetic: Round(137*1.87)=Round(256.19)=256 @ Q0; Q20 Round(307.428)=307 (Havoc Grip shape).
        var item = parser.Parse("""
            Item Class: Gloves
            Rarity: Rare
            Synthetic Ward Gloves
            Runic Gauntlets
            --------
            Ward: 256 (augmented)
            --------
            Item Level: 82
            """);
        var effects = new[]
        {
            Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.IncreasedPercent, 87),
            Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.Added, 21),
        };

        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(ward: (102, 117)),
            effects);

        var ward = Assert.Single(result.Properties);
        Assert.Equal(307m, ward.Value);
        Assert.True(ward.IsQ20);
        Assert.Equal(116, ward.ReconstructedBaseValue);
    }

    [Fact]
    public void Ward_QualityDoesNotIncreaseDefencesControl_IsUnsupported()
    {
        var item = parser.Parse(Item("Ward", 200, quality: 20));
        var control = Effect(ItemPropertyTarget.Ward, ItemPropertyOperation.IncreasedPercent, 0) with
        {
            ResolvedStatIds = ["local_quality_does_not_increase_defences"],
            ReviewedItemPropertySemantic = null,
        };

        var result = calculator.CalculateDefensiveQ20(item, Base(ward: (100, 100)), [control]);

        var ward = Assert.Single(result.Properties);
        Assert.False(ward.IsQ20);
        Assert.Equal(200m, ward.Value);
        Assert.Contains("quality_does_not_increase_defences", ward.UnsupportedReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ward_UnreconstructableDisplayedValue_KeepsClipboardWithoutQ20Claim()
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
        Assert.NotNull(ward.UnsupportedReason);
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

    private static DerivedWeaponModifierEffect UnrelatedEffect(string statId) => new()
    {
        ComponentId = statId,
        IsExactlyResolved = true,
        IsLocal = false,
        HasProvenStatAssociation = true,
        ResolvedStatIds = [statId],
        CanonicalNumericValues = [10m],
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

    private static string BlockItem(decimal value) => $$"""
        Item Class: Shields
        Rarity: Rare
        Test Block Shield
        Golden Buckler
        --------
        Chance to Block: {{value}}%
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

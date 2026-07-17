using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.Derived;

public sealed class DerivedWeaponQ20PropertyCalculatorTests
{
    private readonly ItemTextParser parser = new();
    private readonly DerivedWeaponPropertyCalculator calculator = new();

    [Theory]
    [InlineData(null, 0, true)]
    [InlineData(0, 0, false)]
    [InlineData(10, 10, false)]
    [InlineData(20, 20, false)]
    [InlineData(28, 28, false)]
    public void CalculateQ20_ObservedQualityDoesNotChangeExactQ20PhysicalDps(
        int? quality,
        int expectedObservedQuality,
        bool expectedAssumedZero)
    {
        var result = calculator.CalculateQ20(
            Weapon(quality: quality),
            ReaverAxe(),
            []);

        Assert.Equal(DerivedWeaponQ20Status.Success, result.Q20Status);
        Assert.Equal(46m, result.PhysicalDamage!.Ranges[0].SourceGroup.MinimumValue);
        Assert.Equal(137m, result.PhysicalDamage.Ranges[0].SourceGroup.MaximumValue);
        Assert.Equal(109.8m, result.PhysicalDps);
        Assert.Equal(expectedObservedQuality, result.Q20Provenance!.ObservedQuality);
        Assert.Equal(expectedAssumedZero, result.Q20Provenance.ObservedQualityAssumedZero);
        Assert.Equal(20, result.Q20Provenance.NormalizedQuality);
    }

    [Fact]
    public void CalculateQ20_UsesProvenLocalAddedAndIncreasedPhysicalContributors()
    {
        var result = calculator.CalculateQ20(
            Weapon(),
            ReaverAxe(),
            [
                Effect(
                    "flat",
                    [2m, 6m],
                    ItemPropertyOperation.Added,
                    ["local_minimum_added_physical_damage", "local_maximum_added_physical_damage"]),
                Effect(
                    "percent",
                    [30m],
                    ItemPropertyOperation.IncreasedPercent,
                    ["local_physical_damage_+%"]),
            ]);

        Assert.Equal(DerivedWeaponQ20Status.Success, result.Q20Status);
        Assert.Equal(60m, result.PhysicalDamage!.Ranges[0].SourceGroup.MinimumValue);
        Assert.Equal(180m, result.PhysicalDamage.Ranges[0].SourceGroup.MaximumValue);
        Assert.Equal(144m, result.PhysicalDps);
        Assert.Equal(2, result.Q20Provenance!.ModifierContributions.Count);
    }

    [Fact]
    public void CalculateQ20_TotalKeepsDisplayedElementalAndChaosDps()
    {
        var result = calculator.CalculateQ20(
            Weapon(elemental: "10-20", chaos: "2-4"),
            ReaverAxe(),
            []);

        Assert.Equal(18m, result.ElementalDps);
        Assert.Equal(3.6m, result.ChaosDps);
        Assert.Equal(131.4m, result.TotalDps);
    }

    [Fact]
    public void CalculateQ20_MissingBaseNumericalPropertiesIsExplicitlyUnsupported()
    {
        var result = calculator.CalculateQ20(
            Weapon(),
            ReaverAxe() with { WeaponProperties = null },
            []);

        AssertUnsupported(result, "does not retain an exact sourced Physical Damage");
    }

    [Fact]
    public void CalculateQ20_MalformedQualityIsNotSilentlyAssumedZero()
    {
        var result = calculator.CalculateQ20(
            Weapon(qualityText: "unknown"),
            ReaverAxe(),
            []);

        AssertUnsupported(result, "Observed Quality must contain one percentage integer");
        Assert.Null(result.Q20Provenance!.ObservedQuality);
        Assert.False(result.Q20Provenance.ObservedQualityAssumedZero);
    }

    [Theory]
    [InlineData("local_quality_does_not_increase_physical_damage", "does not increase Physical Damage")]
    [InlineData("local_attack_speed_+%_per_quality", "alternate per-Quality")]
    [InlineData("local_critical_strike_chance_+%_per_quality", "alternate per-Quality")]
    [InlineData("local_added_elemental_damage_per_quality", "alternate per-Quality")]
    [InlineData("local_explicit_modifier_effect_+%", "indirect explicit-modifier-effect")]
    [InlineData("local_physical_damage_always_zero", "set-to-zero")]
    public void CalculateQ20_UnsafeSpecialBehaviorIsExplicitlyUnsupported(
        string statId,
        string reasonFragment)
    {
        var result = calculator.CalculateQ20(
            Weapon(),
            ReaverAxe(),
            [UnsafeEffect(statId)]);

        AssertUnsupported(result, reasonFragment);
    }

    [Fact]
    public void CalculateQ20_PositionalAssociationIsExplicitlyUnsupported()
    {
        var effect = Effect(
            "positional",
            [30m],
            ItemPropertyOperation.IncreasedPercent,
            ["local_physical_damage_+%"] ) with
        {
            HasProvenStatAssociation = false,
            UsesPositionalFallback = true,
        };

        var result = calculator.CalculateQ20(Weapon(), ReaverAxe(), [effect]);

        AssertUnsupported(result, "positional stat association fallback");
    }

    [Fact]
    public void CalculateQ20_UnreviewedLocalPhysicalAssociationIsExplicitlyUnsupported()
    {
        var effect = UnsafeEffect("local_physical_damage_+%") with
        {
            ReviewedItemPropertySemantic = null,
        };

        var result = calculator.CalculateQ20(Weapon(), ReaverAxe(), [effect]);

        AssertUnsupported(result, "without exact reviewed component semantics");
    }

    [Fact]
    public void CalculateQ20_LocalManaLeechFromPhysicalDamageDoesNotPretendToScalePhysicalDamage()
    {
        var manaLeech = UnsafeEffect("local_mana_leech_from_physical_damage_permyriad") with
        {
            ReviewedItemPropertySemantic = null,
        };

        var result = calculator.CalculateQ20(Weapon(), ReaverAxe(), [manaLeech]);

        Assert.Equal(DerivedWeaponQ20Status.Success, result.Q20Status);
        Assert.Equal(109.8m, result.PhysicalDps);
    }

    [Fact]
    public void CalculateQ20_MissingBaseNumericalProvenanceIsExplicitlyUnsupported()
    {
        var itemBase = ReaverAxe() with
        {
            WeaponProperties = ReaverAxe().WeaponProperties! with { Sources = [] },
        };

        var result = calculator.CalculateQ20(Weapon(), itemBase, []);

        AssertUnsupported(result, "no retained provenance");
    }

    [Fact]
    public void CalculateQ20_ReviewedLocalQualityContributionIsExplicitlyUnsupported()
    {
        var effect = Effect(
            "quality",
            [5m],
            ItemPropertyOperation.Added,
            ["local_item_quality_+%"] ) with
        {
            ReviewedItemPropertySemantic = new ItemPropertySemanticDescriptor
            {
                Id = "quality",
                OrderedStatIds = ["local_item_quality_+%"],
                Contributions =
                [
                    new ItemPropertyContribution
                    {
                        Targets = [ItemPropertyTarget.Quality],
                        Operation = ItemPropertyOperation.Added,
                    },
                ],
                Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
            },
        };

        var result = calculator.CalculateQ20(Weapon(), ReaverAxe(), [effect]);

        AssertUnsupported(result, "changes item Quality");
    }

    [Fact]
    public void CalculateQ20_EndpointMidpointRoundsAwayFromZeroBeforeDps()
    {
        var baseItem = ReaverAxe() with
        {
            WeaponProperties = ReaverAxe().WeaponProperties! with
            {
                PhysicalDamageMinimum = 1,
                PhysicalDamageMaximum = 3,
            },
        };
        var result = calculator.CalculateQ20(
            Weapon(physical: "1-3", aps: "1.00"),
            baseItem,
            [Effect(
                "percent",
                [30m],
                ItemPropertyOperation.IncreasedPercent,
                ["local_physical_damage_+%"])]);

        Assert.Equal(2m, result.PhysicalDamage!.Ranges[0].SourceGroup.MinimumValue);
        Assert.Equal(5m, result.PhysicalDamage.Ranges[0].SourceGroup.MaximumValue);
        Assert.Equal(3.5m, result.PhysicalDps);
    }

    private ParsedItem Weapon(
        int? quality = null,
        string? qualityText = null,
        string physical = "38-114",
        string aps = "1.20",
        string? elemental = null,
        string? chaos = null)
    {
        var lines = new List<string>
        {
            "Item Class: One Hand Axes",
            "Rarity: Rare",
            "Q20 Test",
            "Reaver Axe",
            "--------",
            "One Handed Axe",
        };
        if (quality.HasValue || qualityText is not null)
        {
            lines.Add($"Quality: {qualityText ?? $"+{quality}%"}");
        }

        lines.Add($"Physical Damage: {physical}");
        if (elemental is not null)
        {
            lines.Add($"Elemental Damage: {elemental}");
        }

        if (chaos is not null)
        {
            lines.Add($"Chaos Damage: {chaos}");
        }

        lines.Add($"Attacks per Second: {aps}");
        lines.Add("--------");
        lines.Add("Item Level: 85");
        return parser.Parse(string.Join(Environment.NewLine, lines));
    }

    private static ItemBaseRecord ReaverAxe() => new()
    {
        Id = "Metadata/Items/Weapons/OneHandWeapons/OneHandAxes/OneHandAxe18",
        Name = "Reaver Axe",
        ItemClass = "One Hand Axe",
        WeaponProperties = new ItemBaseWeaponProperties
        {
            PhysicalDamageMinimum = 38,
            PhysicalDamageMaximum = 114,
            AttackTimeMilliseconds = 833,
            CriticalStrikeChancePercent = 5m,
            Sources = [new GameDataSourceReference { SourceId = "repoe", ExternalId = "reaver" }],
        },
    };

    private static DerivedWeaponModifierEffect Effect(
        string componentId,
        IReadOnlyList<decimal> values,
        ItemPropertyOperation operation,
        IReadOnlyList<string> statIds) => new()
    {
        ComponentId = componentId,
        SourceModifierIndex = 0,
        ResolvedModifierId = componentId,
        IsExactlyResolved = true,
        IsLocal = true,
        HasProvenStatAssociation = true,
        ResolvedStatIds = statIds,
        CanonicalNumericValues = values,
        ReviewedItemPropertySemantic = new ItemPropertySemanticDescriptor
        {
            Id = componentId,
            OrderedStatIds = statIds,
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Targets = [ItemPropertyTarget.PhysicalDamage],
                    Operation = operation,
                },
            ],
            Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
        },
    };

    private static DerivedWeaponModifierEffect UnsafeEffect(string statId) => new()
    {
        ComponentId = statId,
        ResolvedModifierId = statId,
        IsExactlyResolved = true,
        IsLocal = true,
        HasProvenStatAssociation = true,
        ResolvedStatIds = [statId],
    };

    private static void AssertUnsupported(DerivedWeaponProperties result, string reasonFragment)
    {
        Assert.Equal(DerivedWeaponQ20Status.Unsupported, result.Q20Status);
        Assert.Null(result.PhysicalDps);
        Assert.Null(result.TotalDps);
        Assert.Contains(reasonFragment, result.Q20Provenance!.UnsupportedReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DerivedWeaponPropertyDiagnosticCodes.Q20NormalizationUnsupported);
    }
}

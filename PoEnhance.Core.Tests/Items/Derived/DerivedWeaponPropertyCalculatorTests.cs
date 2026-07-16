using System.Globalization;
using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Tests.Items.Parsing;

namespace PoEnhance.Core.Tests.Items.Derived;

public sealed class DerivedWeaponPropertyCalculatorTests
{
    private readonly ItemTextParser parser = new();
    private readonly DerivedWeaponPropertyCalculator calculator = new();

    [Fact]
    public void Calculate_FlamingReaverAxe_DerivesDisplayedPhysicalElementalAndTotalDps()
    {
        var item = ParseFixture(1);

        var result = calculator.Calculate(item);

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(1.20m, result.AttacksPerSecond);
        Assert.Equal(5.00m, result.CriticalStrikeChance);
        Assert.Equal(76m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(91.2m, result.PhysicalDps);
        Assert.Equal(41.5m, result.ElementalDamage?.AverageHit);
        Assert.Equal(49.8m, result.ElementalDps);
        Assert.Null(result.ChaosDamage);
        Assert.Null(result.ChaosDps);
        Assert.Equal(141.0m, result.TotalDps);
        Assert.Same(Property(item, "physical damage"), result.PhysicalDamage?.SourceProperty);
        Assert.Same(Property(item, "attacks per second"), result.AttacksPerSecondSourceProperty);
        Assert.Same(Property(item, "critical strike chance"), result.CriticalStrikeChanceSourceProperty);
    }

    [Fact]
    public void Calculate_GolemFletch_RetainsOrderedElementalRangesAndDoesNotRound()
    {
        var item = ParseFixture(2);

        var result = calculator.Calculate(item);

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(86.5m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(112.45m, result.PhysicalDps);
        Assert.Equal([104.5m, 63.5m, 82m], result.ElementalDamage?.Ranges.Select(range => range.AverageHit));
        Assert.Equal(250m, result.ElementalDamage?.AverageHit);
        Assert.Equal(325m, result.ElementalDps);
        Assert.Equal(437.45m, result.TotalDps);
        Assert.Equal(1.30m, result.AttacksPerSecond);
        Assert.Equal(6.00m, result.CriticalStrikeChance);
        Assert.Equal(
            Property(item, "elemental damage").NumericGroups,
            result.ElementalDamage?.Ranges.Select(range => range.SourceGroup));
    }

    [Fact]
    public void Calculate_MorbidBite_DerivesPhysicalOnlyDps()
    {
        var result = calculator.Calculate(ParseFixture(3));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(110.5m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(169.065m, result.PhysicalDps);
        Assert.Null(result.ElementalDamage);
        Assert.Null(result.ElementalDps);
        Assert.Null(result.ChaosDps);
        Assert.Equal(169.065m, result.TotalDps);
        Assert.Equal(1.53m, result.AttacksPerSecond);
        Assert.Equal(5.00m, result.CriticalStrikeChance);
    }

    [Fact]
    public void Calculate_WrathCry_UsesDisplayedWeaponPropertiesAndIgnoresSpellDamageModifier()
    {
        var item = ParseFixture(4);
        Assert.Contains(item.Modifiers.SelectMany(modifier => modifier.ValueLines), line =>
            line.Contains("Lightning Damage to Spells", StringComparison.Ordinal));

        var result = calculator.Calculate(item);

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(48m, result.PhysicalDps);
        Assert.Equal(112.8m, result.ElementalDps);
        Assert.Equal(160.8m, result.TotalDps);
        Assert.Equal("Physical Damage: 21-39", result.PhysicalDamage?.SourceProperty.OriginalText);
        Assert.Equal("Elemental Damage: 52-89 (augmented)", result.ElementalDamage?.SourceProperty.OriginalText);
    }

    [Fact]
    public void Calculate_NecroticArmour_IsNotApplicableWithoutDiagnostics()
    {
        var result = calculator.Calculate(ParseFixture(0));

        Assert.Equal(DerivedWeaponPropertyStatus.NotApplicable, result.Status);
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.PhysicalDps);
        Assert.Null(result.ElementalDps);
        Assert.Null(result.ChaosDps);
        Assert.Null(result.TotalDps);
        Assert.Null(result.AttacksPerSecond);
    }

    [Fact]
    public void Calculate_WeaponDamageWithoutAttacksPerSecond_IsInvalid()
    {
        var item = ParseWeaponProperties("Physical Damage: 10-20");

        var result = calculator.Calculate(item);

        Assert.Equal(DerivedWeaponPropertyStatus.Invalid, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DerivedWeaponPropertyDiagnosticCodes.MissingAttacksPerSecond, diagnostic.Code);
        Assert.Null(diagnostic.SourceProperty);
        Assert.Null(result.TotalDps);
    }

    [Fact]
    public void Calculate_AttacksPerSecondWithoutDisplayedDamage_IsInvalid()
    {
        var result = calculator.Calculate(ParseWeaponProperties("Attacks per Second: 2.00"));

        Assert.Equal(DerivedWeaponPropertyStatus.Invalid, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DerivedWeaponPropertyDiagnosticCodes.MissingDamage, diagnostic.Code);
        Assert.Null(diagnostic.SourceProperty);
        Assert.Equal(2.00m, result.AttacksPerSecond);
        Assert.Null(result.TotalDps);
    }

    [Fact]
    public void Calculate_MalformedAttacksPerSecond_IsUnsupportedAndRetainsSource()
    {
        var item = ParseWeaponProperties(
            "Physical Damage: 10-20",
            "Attacks per Second: fast");

        var result = calculator.Calculate(item);

        Assert.Equal(DerivedWeaponPropertyStatus.Unsupported, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DerivedWeaponPropertyDiagnosticCodes.UnsupportedAttacksPerSecond, diagnostic.Code);
        Assert.Same(Property(item, "attacks per second"), diagnostic.SourceProperty);
        Assert.Empty(diagnostic.SourceProperty!.NumericGroups);
        Assert.Null(result.TotalDps);
    }

    [Fact]
    public void Calculate_MalformedDamageRange_IsUnsupportedAndNeverPartiallyParsed()
    {
        var item = ParseWeaponProperties(
            "Physical Damage: 10 to 20",
            "Attacks per Second: 2.00");

        var result = calculator.Calculate(item);

        Assert.Equal(DerivedWeaponPropertyStatus.Unsupported, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DerivedWeaponPropertyDiagnosticCodes.UnsupportedDamage, diagnostic.Code);
        Assert.Equal("10 to 20", diagnostic.SourceProperty?.RawValueText);
        Assert.Empty(diagnostic.SourceProperty!.NumericGroups);
        Assert.Null(result.PhysicalDps);
        Assert.Null(result.TotalDps);

        var scalarItem = ParseWeaponProperties(
            "Physical Damage: 15",
            "Attacks per Second: 2.00");
        Assert.True(Assert.Single(Property(scalarItem, "physical damage").NumericGroups).IsScalar);

        var scalarResult = calculator.Calculate(scalarItem);

        Assert.Equal(DerivedWeaponPropertyStatus.Unsupported, scalarResult.Status);
        Assert.Equal(
            DerivedWeaponPropertyDiagnosticCodes.UnsupportedDamage,
            Assert.Single(scalarResult.Diagnostics).Code);
        Assert.Null(scalarResult.TotalDps);
    }

    [Fact]
    public void Calculate_PhysicalOnlyWeapon_UsesOneDisplayedRange()
    {
        var result = calculator.Calculate(ParseWeaponProperties(
            "Physical Damage: 10-20",
            "Attacks per Second: 2.00"));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(15m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(30m, result.PhysicalDps);
        Assert.Null(result.ElementalDps);
        Assert.Null(result.ChaosDps);
        Assert.Equal(30m, result.TotalDps);
    }

    [Fact]
    public void Calculate_ElementalOnlyWeapon_SumsOrderedDisplayedRanges()
    {
        var result = calculator.Calculate(ParseWeaponProperties(
            "Elemental Damage: 10-20, 1-2",
            "Attacks per Second: 2.00"));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Null(result.PhysicalDps);
        Assert.Equal([15m, 1.5m], result.ElementalDamage?.Ranges.Select(range => range.AverageHit));
        Assert.Equal(16.5m, result.ElementalDamage?.AverageHit);
        Assert.Equal(33m, result.ElementalDps);
        Assert.Equal(33m, result.TotalDps);
    }

    [Fact]
    public void Calculate_OptionalChaosDamage_SumsRangesAndContributesToTotal()
    {
        var item = ParseWeaponProperties(
            "Chaos Damage: 10-20, 5-7",
            "Attacks per Second: 2.00");

        var result = calculator.Calculate(item);

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Null(result.PhysicalDps);
        Assert.Null(result.ElementalDps);
        Assert.Equal([15m, 6m], result.ChaosDamage?.Ranges.Select(range => range.AverageHit));
        Assert.Equal(21m, result.ChaosDamage?.AverageHit);
        Assert.Equal(42m, result.ChaosDps);
        Assert.Equal(42m, result.TotalDps);
        Assert.Same(Property(item, "chaos damage"), result.ChaosDamage?.SourceProperty);
    }

    [Fact]
    public void Calculate_IsInvariantUnderPolishCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

            var result = calculator.Calculate(ParseFixture(2));

            Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
            Assert.Equal(112.45m, result.PhysicalDps);
            Assert.Equal(325m, result.ElementalDps);
            Assert.Equal(437.45m, result.TotalDps);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Calculate_DoesNotRoundIntermediateAverageOrDps()
    {
        var result = calculator.Calculate(ParseWeaponProperties(
            "Physical Damage: 1-2",
            "Attacks per Second: 1.333"));

        Assert.Equal(DerivedWeaponPropertyStatus.Success, result.Status);
        Assert.Equal(1.5m, result.PhysicalDamage?.AverageHit);
        Assert.Equal(1.9995m, result.PhysicalDps);
        Assert.Equal(1.9995m, result.TotalDps);
    }

    private ParsedItem ParseFixture(int index)
    {
        return parser.Parse(CopiedItemCorpus.LoadItems()[index]);
    }

    private ParsedItem ParseWeaponProperties(params string[] propertyLines)
    {
        return parser.Parse($$"""
Item Class: One Hand Axes
Rarity: Rare
Test Weapon
Reaver Axe
--------
{{string.Join(Environment.NewLine, propertyLines)}}
--------
Item Level: 85
""");
    }

    private static ParsedItemProperty Property(ParsedItem item, string normalizedName)
    {
        return Assert.Single(item.Properties, property => property.NormalizedName == normalizedName);
    }
}

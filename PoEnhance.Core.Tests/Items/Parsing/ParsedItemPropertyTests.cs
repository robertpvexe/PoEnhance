using System.Globalization;
using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Tests.Items.Parsing;

public sealed class ParsedItemPropertyTests
{
    private readonly ItemTextParser parser = new();

    [Fact]
    public void Parse_FlamingReaverAxe_CreatesStructuredWeaponProperties()
    {
        var item = ParseFixture(1);

        var physical = Property(item, "physical damage");
        Assert.Equal("Physical Damage: 38-114", physical.OriginalText);
        Assert.Equal("Physical Damage", physical.Name);
        Assert.Equal("38-114", physical.RawValueText);
        Assert.Equal("physical damage", physical.NormalizedName);
        var physicalRange = Assert.Single(physical.NumericGroups);
        Assert.Equal("38-114", physicalRange.OriginalText);
        AssertRange(physicalRange, 38m, 114m, isAugmented: false);

        var elemental = Property(item, "elemental damage");
        var elementalRange = Assert.Single(elemental.NumericGroups);
        Assert.Equal("26-57 (augmented)", elementalRange.OriginalText);
        AssertRange(elementalRange, 26m, 57m, isAugmented: true);

        var criticalStrikeChance = Property(item, "critical strike chance");
        AssertScalar(
            Assert.Single(criticalStrikeChance.NumericGroups),
            5.00m,
            isPercentage: true,
            isAugmented: false);

        var attacksPerSecond = Property(item, "attacks per second");
        AssertScalar(
            Assert.Single(attacksPerSecond.NumericGroups),
            1.20m,
            isPercentage: false,
            isAugmented: false);
    }

    [Fact]
    public void Parse_GolemFletch_RetainsThreeOrderedAugmentedElementalRangesWithoutDerivedValues()
    {
        var item = ParseFixture(2);

        var elemental = Property(item, "elemental damage");
        Assert.Equal(
            "70-139 (augmented), 46-81 (augmented), 9-155 (augmented)",
            elemental.RawValueText);
        Assert.Collection(
            elemental.NumericGroups,
            group =>
            {
                Assert.Equal("70-139 (augmented)", group.OriginalText);
                AssertRange(group, 70m, 139m, isAugmented: true);
            },
            group =>
            {
                Assert.Equal("46-81 (augmented)", group.OriginalText);
                AssertRange(group, 46m, 81m, isAugmented: true);
            },
            group =>
            {
                Assert.Equal("9-155 (augmented)", group.OriginalText);
                AssertRange(group, 9m, 155m, isAugmented: true);
            });
    }

    [Fact]
    public void Parse_MorbidBite_RetainsAugmentedRangeScalarAndOriginalPropertyLines()
    {
        var item = ParseFixture(3);

        AssertRange(
            Assert.Single(Property(item, "physical damage").NumericGroups),
            61m,
            160m,
            isAugmented: true);
        AssertScalar(
            Assert.Single(Property(item, "attacks per second").NumericGroups),
            1.53m,
            isPercentage: false,
            isAugmented: true);
        Assert.Equal(
            item.Properties.Select(property => property.OriginalText),
            item.PropertyLines);
        Assert.Contains("Physical Damage: 61-160 (augmented)", item.PropertyLines);
        Assert.Contains("Attacks per Second: 1.53 (augmented)", item.PropertyLines);
    }

    [Fact]
    public void Parse_NecroticArmour_CreatesGenericDefenceScalarsAndStableSourceOrder()
    {
        var item = ParseFixture(0);

        AssertScalar(
            Assert.Single(Property(item, "evasion rating").NumericGroups),
            828m,
            isPercentage: false,
            isAugmented: false);
        AssertScalar(
            Assert.Single(Property(item, "energy shield").NumericGroups),
            166m,
            isPercentage: false,
            isAugmented: false);
        Assert.Equal(Enumerable.Range(0, item.Properties.Count), item.Properties.Select(property => property.SourceIndex));
    }

    [Fact]
    public void Parse_SupremeSpikedShield_CreatesBlockAndDefenceScalars()
    {
        var item = ParseFixture(13);

        AssertScalar(
            Assert.Single(Property(item, "chance to block").NumericGroups),
            24m,
            isPercentage: true,
            isAugmented: false);
        AssertScalar(
            Assert.Single(Property(item, "evasion rating").NumericGroups),
            429m,
            isPercentage: false,
            isAugmented: true);
        AssertScalar(
            Assert.Single(Property(item, "energy shield").NumericGroups),
            124m,
            isPercentage: false,
            isAugmented: true);
    }

    [Fact]
    public void Parse_Quality_CreatesSignedAugmentedPercentageScalar()
    {
        var item = ParseFixture(9);

        var quality = Property(item, "quality");
        Assert.Equal("Quality: +20% (augmented)", quality.OriginalText);
        var group = Assert.Single(quality.NumericGroups);
        Assert.Equal("+20% (augmented)", group.OriginalText);
        AssertScalar(group, 20m, isPercentage: true, isAugmented: true);
    }

    [Fact]
    public void Parse_UnsupportedPropertyValuesRemainLosslessWithoutNumericGroups()
    {
        var item = ParseFixture(1);

        var sockets = Property(item, "sockets");
        Assert.Equal("Sockets: R", sockets.OriginalText);
        Assert.Equal("Sockets", sockets.Name);
        Assert.Equal("R", sockets.RawValueText);
        Assert.Empty(sockets.NumericGroups);

        var weaponRange = Property(item, "weapon range");
        Assert.Equal("Weapon Range: 1.1 metres", weaponRange.OriginalText);
        Assert.Equal("1.1 metres", weaponRange.RawValueText);
        Assert.Empty(weaponRange.NumericGroups);
    }

    [Fact]
    public void Parse_StructuredNumericPropertiesAreInvariantUnderPolishCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

            var item = ParseFixture(2);

            Assert.Equal(
                [70m, 46m, 9m],
                Property(item, "elemental damage").NumericGroups.Select(group => group.MinimumValue));
            Assert.Equal(
                [139m, 81m, 155m],
                Property(item, "elemental damage").NumericGroups.Select(group => group.MaximumValue));
            Assert.Equal(1.30m, Assert.Single(Property(item, "attacks per second").NumericGroups).ScalarValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Parse_AdvancedModifierTierAndRankRemainOnModifiersNotProperties()
    {
        var flamingAxe = ParseFixture(1);
        var flaming = Assert.Single(flamingAxe.Modifiers, modifier => modifier.Name == "Flaming");
        Assert.Equal(6, flaming.Tier);
        Assert.Null(flaming.Rank);
        Assert.Equal(
            "{ Prefix Modifier \"Flaming\" (Tier: 6) \u2014 Damage, Elemental, Fire, Attack }",
            flaming.RawMetadataLine);

        var bodyArmour = parser.Parse(ReadSample("advanced-rare-body-armour.txt"));
        var upgraded = Assert.Single(bodyArmour.Modifiers, modifier => modifier.Name == "Upgraded");
        Assert.Null(upgraded.Tier);
        Assert.Equal(1, upgraded.Rank);
        Assert.True(upgraded.IsCrafted);
        Assert.DoesNotContain(bodyArmour.Properties, property =>
            property.Name.Contains("Tier", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Rank", StringComparison.OrdinalIgnoreCase));
    }

    private ParsedItem ParseFixture(int index)
    {
        return parser.Parse(CopiedItemCorpus.LoadItems()[index]);
    }

    private static ParsedItemProperty Property(ParsedItem item, string normalizedName)
    {
        return Assert.Single(item.Properties, property => property.NormalizedName == normalizedName);
    }

    private static void AssertScalar(
        ParsedItemPropertyNumericGroup group,
        decimal expected,
        bool isPercentage,
        bool isAugmented)
    {
        Assert.True(group.IsScalar);
        Assert.False(group.IsRange);
        Assert.Equal(expected, group.ScalarValue);
        Assert.Null(group.MinimumValue);
        Assert.Null(group.MaximumValue);
        Assert.Equal(isPercentage, group.IsPercentage);
        Assert.Equal(isAugmented, group.IsAugmented);
    }

    private static void AssertRange(
        ParsedItemPropertyNumericGroup group,
        decimal expectedMinimum,
        decimal expectedMaximum,
        bool isAugmented)
    {
        Assert.False(group.IsScalar);
        Assert.True(group.IsRange);
        Assert.Null(group.ScalarValue);
        Assert.Equal(expectedMinimum, group.MinimumValue);
        Assert.Equal(expectedMaximum, group.MaximumValue);
        Assert.False(group.IsPercentage);
        Assert.Equal(isAugmented, group.IsAugmented);
    }

    private static string ReadSample(string fileName)
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "Items", fileName);
        return File.ReadAllText(samplePath);
    }
}

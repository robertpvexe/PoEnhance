using System.Collections.Immutable;
using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchBaseRollTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Theory]
    [InlineData(100, 0)]
    [InlineData(200, 100)]
    [InlineData(183, 83)]
    public void CreateDraft_ExposesExactNaturalBaseRollPercentile(int armour, int expectedPercentile)
    {
        var draft = CreateArmourDraft(armour, quality: 0, Defence(armour: (100, 200)));

        Assert.Equal(expectedPercentile, draft.BaseRollPercentile);
    }

    [Fact]
    public void Calculate_FixedRangeAndMissingProvenanceAreOmitted()
    {
        var source = SourceProperty();

        Assert.Null(DerivedBaseRollPercentileCalculator.Calculate(Properties(
            Property(ItemPropertyTarget.Armour, 100, (100, 100), source))));
        Assert.Null(DerivedBaseRollPercentileCalculator.Calculate(Properties(
            Property(ItemPropertyTarget.Armour, 150, (100, 200), source, sourced: false))));
    }

    [Fact]
    public void Calculate_MissingBaseRangeIsOmitted()
    {
        var property = Property(ItemPropertyTarget.Armour, 150, (100, 200), SourceProperty()) with
        {
            BaseProperties = null,
        };

        Assert.Null(DerivedBaseRollPercentileCalculator.Calculate(Properties(property)));
    }

    [Fact]
    public void Calculate_AmbiguousReconstructedRollIsOmitted()
    {
        var property = Property(ItemPropertyTarget.Armour, 150, (100, 200), SourceProperty()) with
        {
            ReconstructedBaseValue = null,
            UnsupportedReason = "ambiguous reconstructed base roll",
        };

        Assert.Null(DerivedBaseRollPercentileCalculator.Calculate(Properties(property)));
    }

    [Fact]
    public void Calculate_ConflictingHybridPercentilesAreOmittedWithoutAveraging()
    {
        var source = SourceProperty();
        var properties = Properties(
            Property(ItemPropertyTarget.Armour, 150, (100, 200), source),
            Property(ItemPropertyTarget.Evasion, 250, (200, 400), source));

        Assert.Null(DerivedBaseRollPercentileCalculator.Calculate(properties));
    }

    [Fact]
    public void Calculate_MatchingHybridPercentilesExposeOneExactValue()
    {
        var source = SourceProperty();
        var properties = Properties(
            Property(ItemPropertyTarget.Armour, 150, (100, 200), source),
            Property(ItemPropertyTarget.Evasion, 300, (200, 400), source));

        Assert.Equal(50m, DerivedBaseRollPercentileCalculator.Calculate(properties));
    }

    [Fact]
    public void Calculate_LocalEffectsAndDisplayedValueDoNotDistortNaturalRoll()
    {
        var property = Property(ItemPropertyTarget.Armour, 150, (100, 200), SourceProperty()) with
        {
            Value = 275m,
            LocalAdded = 25m,
            LocalIncreasedPercent = 50m,
        };

        Assert.Equal(50m, DerivedBaseRollPercentileCalculator.Calculate(Properties(property)));
    }

    [Fact]
    public void CreateDraft_ObservedAndRequestedQualityDoNotAffectBaseRollPercentile()
    {
        var unquality = CreateArmourDraft(183, quality: 0, Defence(armour: (100, 200)));
        var qualityTwenty = CreateArmourDraft(220, quality: 20, Defence(armour: (100, 200)));
        var qualityFilter = Assert.Single(qualityTwenty.RequestedItemFilters,
            filter => filter.Kind == TradeSearchRequestedItemFilterKind.Quality);
        var requestedQuality = qualityTwenty with
        {
            RequestedItemFilters = qualityTwenty.RequestedItemFilters
                .Select(filter => filter.Kind == TradeSearchRequestedItemFilterKind.Quality
                    ? TradeSearchDraftMapper.ParseRequestedItemFilterText(qualityFilter, "28", true)
                    : filter)
                .ToImmutableArray(),
        };

        Assert.Equal(83m, unquality.BaseRollPercentile);
        Assert.Equal(unquality.BaseRollPercentile, qualityTwenty.BaseRollPercentile);
        Assert.Equal(qualityTwenty.BaseRollPercentile, requestedQuality.BaseRollPercentile);
    }

    private TradeSearchDraft CreateArmourDraft(
        int displayedArmour,
        int quality,
        ItemBaseDefenceProperties defence)
    {
        var item = parser.Parse($$"""
            Item Class: Body Armours
            Rarity: Rare
            Test Shell
            Test Base
            --------
            Quality: +{{quality}}%
            Armour: {{displayedArmour}}
            --------
            Item Level: 85
            """);
        var itemBase = new ItemBaseRecord
        {
            Id = "base.test",
            Name = "Test Base",
            ItemClass = "Body Armour",
            DefenceProperties = defence,
        };
        var result = mapper.CreateDraft(item, new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = itemBase,
            ResolvedBaseId = itemBase.Id,
            ResolvedBaseName = itemBase.Name,
            Candidates = [itemBase],
        });

        Assert.True(result.IsSuccess);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private ParsedItemProperty SourceProperty() => parser.Parse("""
        Item Class: Body Armours
        Rarity: Rare
        Test Shell
        Test Base
        --------
        Armour: 150
        --------
        Item Level: 85
        """).Properties.Single(property => property.NormalizedName == "armour");

    private static DerivedDefensiveProperties Properties(params DerivedDefensiveProperty[] values) =>
        new() { Properties = values };

    private static DerivedDefensiveProperty Property(
        ItemPropertyTarget target,
        int baseRoll,
        (int Minimum, int Maximum) range,
        ParsedItemProperty source,
        bool sourced = true) => new()
        {
            Target = target,
            Value = baseRoll,
            SourceProperty = source,
            ReconstructedBaseValue = baseRoll,
            BaseProperties = target switch
            {
                ItemPropertyTarget.Armour => Defence(armour: range, sourced: sourced),
                ItemPropertyTarget.Evasion => Defence(evasion: range, sourced: sourced),
                _ => Defence(sourced: sourced),
            },
        };

    private static ItemBaseDefenceProperties Defence(
        (int Minimum, int Maximum)? armour = null,
        (int Minimum, int Maximum)? evasion = null,
        bool sourced = true) => new()
        {
            ArmourMinimum = armour?.Minimum,
            ArmourMaximum = armour?.Maximum,
            EvasionRatingMinimum = evasion?.Minimum,
            EvasionRatingMaximum = evasion?.Maximum,
            Sources = sourced
                ? [new GameDataSourceReference { SourceId = "test", ExternalId = "base.test" }]
                : [],
        };
}

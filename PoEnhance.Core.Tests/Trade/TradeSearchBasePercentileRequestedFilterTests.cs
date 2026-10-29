using System.Collections.Immutable;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchBasePercentileRequestedFilterTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public void CreateDraft_ReliableSingleDefenceBaseSeedsInactiveBasePercentileFromDerivedRoll()
    {
        var draft = CreateArmourDraft(
            "Rare",
            armour: 183,
            quality: 0,
            Defence(armour: (100, 200)));

        Assert.Equal(83m, draft.BaseRollPercentile);
        var filter = Filter(draft, TradeSearchRequestedItemFilterKind.BasePercentile);
        Assert.Equal(83, filter.ObservedValue);
        Assert.Equal("83", filter.CurrentText);
        Assert.Equal(83, filter.RequestedMinimum);
        Assert.False(filter.IsActive);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Valid, filter.LocalValidationStatus);
        Assert.Equal(
            [
                TradeSearchRequestedItemFilterKind.ItemLevel,
                TradeSearchRequestedItemFilterKind.Quality,
                TradeSearchRequestedItemFilterKind.BasePercentile,
            ],
            draft.RequestedItemFilters.Select(item => item.Kind));
    }

    [Fact]
    public void CreateDraft_SupportedHybridWithMatchingProvenanceSeedsOneInactiveBasePercentile()
    {
        var draft = CreateArmourDraft(
            "Rare",
            armour: 150,
            quality: 0,
            Defence(armour: (100, 200), evasion: (200, 400)),
            evasion: 300);

        Assert.Equal(50m, draft.BaseRollPercentile);
        var filter = Filter(draft, TradeSearchRequestedItemFilterKind.BasePercentile);
        Assert.Equal(50, filter.ObservedValue);
        Assert.Equal(50, filter.RequestedMinimum);
        Assert.False(filter.IsActive);
    }

    [Fact]
    public void CreateDraft_ConflictingHybridOmitsBasePercentileWithoutFabricatingCriterion()
    {
        var draft = CreateArmourDraft(
            "Rare",
            armour: 150,
            quality: 0,
            Defence(armour: (100, 200), evasion: (200, 400)),
            evasion: 250);

        Assert.Null(draft.BaseRollPercentile);
        Assert.DoesNotContain(
            draft.RequestedItemFilters,
            filter => filter.Kind == TradeSearchRequestedItemFilterKind.BasePercentile);
    }

    [Fact]
    public void CreateDraft_UnknownOrUnsoursedDefenceOmitsBasePercentile()
    {
        var unsourced = CreateArmourDraft(
            "Rare",
            armour: 150,
            quality: 0,
            Defence(armour: (100, 200), sourced: false));
        var weapon = CreateDraft("""
            Item Class: One Hand Axes
            Rarity: Rare
            Header Test
            Reaver Axe
            --------
            Item Level: 85
            """);

        Assert.Null(unsourced.BaseRollPercentile);
        Assert.DoesNotContain(
            unsourced.RequestedItemFilters,
            filter => filter.Kind == TradeSearchRequestedItemFilterKind.BasePercentile);
        Assert.Null(weapon.BaseRollPercentile);
        Assert.DoesNotContain(
            weapon.RequestedItemFilters,
            filter => filter.Kind == TradeSearchRequestedItemFilterKind.BasePercentile);
    }

    [Theory]
    [InlineData("Normal")]
    [InlineData("Magic")]
    [InlineData("Rare")]
    public void CreateDraft_BasePercentileAlwaysStartsInactiveForOrdinaryRarities(string rarity)
    {
        var draft = CreateArmourDraft(
            rarity,
            armour: 256,
            quality: 28,
            Defence(armour: (100, 200)));

        Assert.Equal(100m, draft.BaseRollPercentile);
        Assert.False(Filter(draft, TradeSearchRequestedItemFilterKind.BasePercentile).IsActive);
        Assert.True(Filter(draft, TradeSearchRequestedItemFilterKind.Quality).IsActive);
    }

    [Fact]
    public void ParseRequestedText_EditingBasePercentileMinDoesNotAutoActivate()
    {
        var source = Filter(
            CreateArmourDraft("Rare", armour: 183, quality: 0, Defence(armour: (100, 200))),
            TradeSearchRequestedItemFilterKind.BasePercentile);

        var edited = TradeSearchDraftMapper.ParseRequestedItemFilterText(source, "90");

        Assert.False(edited.IsActive);
        Assert.Equal(90, edited.RequestedMinimum);
        Assert.Equal(83, edited.ObservedValue);
    }

    [Fact]
    public void CreateDraft_FreshDraftResetsPreviouslyActivatedBasePercentileToOff()
    {
        var first = CreateArmourDraft("Rare", armour: 183, quality: 0, Defence(armour: (100, 200)));
        var activated = TradeSearchDraftMapper.ParseRequestedItemFilterText(
            Filter(first, TradeSearchRequestedItemFilterKind.BasePercentile),
            "90",
            isActive: true);
        Assert.True(activated.IsActive);
        Assert.Equal(90, activated.RequestedMinimum);

        var next = CreateArmourDraft("Magic", armour: 150, quality: 0, Defence(armour: (100, 200)));
        var reset = Filter(next, TradeSearchRequestedItemFilterKind.BasePercentile);

        Assert.False(reset.IsActive);
        Assert.Equal(50, reset.RequestedMinimum);
        Assert.Equal("50", reset.CurrentText);
    }

    private TradeSearchDraft CreateDraft(string text)
    {
        var result = mapper.CreateDraft(parser.Parse(text));
        Assert.True(result.IsSuccess);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private TradeSearchDraft CreateArmourDraft(
        string rarity,
        int armour,
        int quality,
        ItemBaseDefenceProperties defence,
        int? evasion = null)
    {
        var qualityLine = quality > 0 ? $"Quality: +{quality}%\n" : string.Empty;
        var evasionLine = evasion.HasValue ? $"Evasion Rating: {evasion.Value}\n" : string.Empty;
        var item = parser.Parse($$"""
            Item Class: Body Armours
            Rarity: {{rarity}}
            Test Shell
            Test Base
            --------
            {{qualityLine}}Armour: {{armour}}
            {{evasionLine}}--------
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

    private static TradeSearchRequestedItemFilter Filter(
        TradeSearchDraft draft,
        TradeSearchRequestedItemFilterKind kind) =>
        Assert.Single(draft.RequestedItemFilters, filter => filter.Kind == kind);

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

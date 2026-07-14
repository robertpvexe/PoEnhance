using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemIdentityMapperTests
{
    private readonly PathOfExileTradeItemIdentityMapper mapper = new();

    [Fact]
    public void Map_VoicesMatchesExactCanonicalNameAndBase()
    {
        var result = Map(Draft("Voices", "Large Cluster Jewel"), Catalog(
            Unique("Voices", "Large Cluster Jewel", "jewel")));

        Assert.True(result.IsSuccess);
        Assert.Equal("Voices", result.Identity?.CanonicalName);
        Assert.Equal("Large Cluster Jewel", result.Identity?.CanonicalType);
        Assert.Equal(TradeTriState.No, result.Identity?.Foulborn);
    }

    [Fact]
    public void Map_MoonbendersWingMatchesExactCanonicalNameAndTomahawk()
    {
        var result = Map(Draft("Moonbender's Wing", "Tomahawk"), Catalog(
            Unique("Moonbender's Wing", "Tomahawk", "weapon")));

        Assert.True(result.IsSuccess);
        Assert.Equal("Moonbender's Wing", result.Identity?.CanonicalName);
        Assert.Equal("Tomahawk", result.Identity?.CanonicalType);
    }

    [Fact]
    public void Map_SameNameWithIncompatibleBaseIsRejected()
    {
        var result = Map(Draft("Moonbender's Wing", "Driftwood Axe"), Catalog(
            Unique("Moonbender's Wing", "Tomahawk", "weapon")));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueIdentity,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_FoulbornMoonbendersWingUsesUnderlyingCanonicalIdentityAndFoulbornYes()
    {
        var result = Map(Draft("Foulborn Moonbender's Wing", "Tomahawk"), Catalog(
            Unique("Moonbender's Wing", "Tomahawk", "weapon")));

        Assert.True(result.IsSuccess);
        Assert.Equal("Moonbender's Wing", result.Identity?.CanonicalName);
        Assert.Equal("Tomahawk", result.Identity?.CanonicalType);
        Assert.Equal(TradeTriState.Yes, result.Identity?.Foulborn);
    }

    [Fact]
    public void Map_OrdinaryMoonbendersWingAutoDefaultsFoulbornNo()
    {
        var result = Map(Draft("Moonbender's Wing", "Tomahawk"), Catalog(
            Unique("Moonbender's Wing", "Tomahawk", "weapon")));

        Assert.True(result.IsSuccess);
        Assert.Equal(TradeTriState.No, result.Identity?.Foulborn);
    }

    [Theory]
    [InlineData(TradeTriState.Any)]
    [InlineData(TradeTriState.Yes)]
    [InlineData(TradeTriState.No)]
    public void Map_ExplicitFoulbornCriteriaOverridesAutoDetection(TradeTriState requested)
    {
        var result = Map(
            Draft("Foulborn Moonbender's Wing", "Tomahawk") with
            {
                ItemVariantCriteria = new TradeItemVariantCriteria { Foulborn = requested },
            },
            Catalog(Unique("Moonbender's Wing", "Tomahawk", "weapon")));

        Assert.True(result.IsSuccess);
        Assert.Equal(requested, result.Identity?.Foulborn);
    }

    [Fact]
    public void Map_ExactFullNameIsTriedBeforeFoulbornPrefixRemoval()
    {
        var result = Map(Draft("Foulborn Moonbender's Wing", "Tomahawk"), Catalog(
            Unique("Foulborn Moonbender's Wing", "Tomahawk", "weapon"),
            Unique("Moonbender's Wing", "Tomahawk", "weapon")));

        Assert.True(result.IsSuccess);
        Assert.Equal("Foulborn Moonbender's Wing", result.Identity?.CanonicalName);
        Assert.Equal(TradeTriState.No, result.Identity?.Foulborn);
    }

    [Fact]
    public void Map_RealFoulbornCurrencyNameIsNotStrippedForUniqueIdentity()
    {
        var result = Map(Draft("Foulborn Orb", "Tomahawk"), Catalog(
            Entry("Foulborn Orb", "Foulborn Orb", false, "currency"),
            Unique("Orb", "Tomahawk", "weapon")));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueIdentity,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_UnknownDecoratedNameIsRejectedLocally()
    {
        var result = Map(Draft("Foulborn Not A Real Unique", "Tomahawk"), Catalog(
            Unique("Moonbender's Wing", "Tomahawk", "weapon")));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueDisplayVariant,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_GenericFirstWordRemovalIsImpossible()
    {
        var result = Map(Draft("Glorious Moonbender's Wing", "Tomahawk"), Catalog(
            Unique("Moonbender's Wing", "Tomahawk", "weapon")));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueIdentity,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void TradeItemVariantCriteriaCanGrowWithExplicitPropertiesWithoutChangingQueryOrchestration()
    {
        var criteria = new TradeItemVariantCriteria { Foulborn = TradeTriState.Any };

        Assert.Equal(TradeTriState.Any, criteria.Foulborn);
        Assert.Equal(TradeTriState.Auto, new TradeItemVariantCriteria().Foulborn);
    }

    private PathOfExileTradeItemIdentityMappingResult Map(
        TradeSearchDraft draft,
        PathOfExileTradeItemCatalog catalog)
    {
        return mapper.Map(draft, catalog);
    }

    private static TradeSearchDraft Draft(string displayName, string baseType)
    {
        return new TradeSearchDraft
        {
            ItemClass = "One Hand Axes",
            Rarity = "Unique",
            DisplayName = displayName,
            ParsedBaseType = baseType,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.test",
                ResolvedBaseName = baseType,
            },
        };
    }

    private static PathOfExileTradeItemCatalog Catalog(params PathOfExileTradeItemEntry[] entries)
    {
        return new PathOfExileTradeItemCatalog(entries);
    }

    private static PathOfExileTradeItemEntry Unique(
        string name,
        string type,
        string groupId)
    {
        return Entry(name, type, isUnique: true, groupId);
    }

    private static PathOfExileTradeItemEntry Entry(
        string? name,
        string type,
        bool isUnique,
        string groupId)
    {
        return new PathOfExileTradeItemEntry
        {
            ProviderOrder = 0,
            GroupId = groupId,
            GroupLabel = groupId,
            Name = name,
            Type = type,
            IsUnique = isUnique,
        };
    }
}

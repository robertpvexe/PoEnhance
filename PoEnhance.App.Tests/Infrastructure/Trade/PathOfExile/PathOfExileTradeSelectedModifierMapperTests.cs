using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeSelectedModifierMapperTests
{
    private readonly PathOfExileTradeSelectedModifierMapper mapper = new(new PathOfExileTradeStatMatcher());

    [Fact]
    public void Map_NoSelectedModifiersDoesNotRequireCatalog()
    {
        var result = mapper.Map(
            [Modifier("+55 to maximum Life", isSelected: false)],
            catalog: null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Map_ExactSelectedModifiersPreservesSelectedOrderIdsAndExtractedValues()
    {
        var catalog = Catalog(
            Entry("explicit.stat_life", "+# to maximum Life", "explicit"),
            Entry("explicit.stat_fire", "Adds # to # Fire Damage", "explicit"));

        var result = mapper.Map(
            [
                Modifier("+55 to maximum Life"),
                Modifier("Adds 10 to 20 Fire Damage"),
            ],
            catalog);

        Assert.True(result.IsSuccess);
        Assert.Equal(["explicit.stat_life", "explicit.stat_fire"], result.Filters.Select(filter => filter.StatId));
        Assert.Equal([55m], result.Filters[0].ExtractedNumericValues);
        Assert.Equal([10m, 20m], result.Filters[1].ExtractedNumericValues);
        Assert.Equal([0, 1], result.Filters.Select(filter => filter.SourceIndex));
    }

    [Fact]
    public void Map_UnselectedModifiersAreNotMatchedOrSerialized()
    {
        var catalog = Catalog(Entry("explicit.stat_life", "+# to maximum Life", "explicit"));

        var result = mapper.Map(
            [
                Modifier("+55 to maximum Life", isSelected: false),
                Modifier("+21 to maximum Life"),
            ],
            catalog);

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal(1, filter.SourceIndex);
        Assert.Equal([21m], filter.ExtractedNumericValues);
    }

    [Fact]
    public void Map_AmbiguousSelectedModifierFailsWholeMappingWithoutChoosingFirst()
    {
        var catalog = Catalog(
            Entry("explicit.stat_life_one", "+# to maximum Life", "explicit"),
            Entry("explicit.stat_life_two", "+# to maximum Life", "explicit"));

        var result = mapper.Map([Modifier("+55 to maximum Life")], catalog);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_NotFoundSelectedModifierFailsWholeMapping()
    {
        var catalog = Catalog(Entry("explicit.stat_mana", "+# to maximum Mana", "explicit"));

        var result = mapper.Map([Modifier("+55 to maximum Life")], catalog);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_InvalidSelectedModifierFailsWholeMapping()
    {
        var result = mapper.Map(
            [Modifier(" ")],
            Catalog(Entry("explicit.stat_life", "+# to maximum Life", "explicit")));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.InvalidInput,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_KindMismatchFailsWholeMapping()
    {
        var catalog = Catalog(Entry("explicit.stat_life", "+# to maximum Life", "explicit"));

        var result = mapper.Map(
            [Modifier("+55 to maximum Life", kind: ParsedModifierKind.Implicit)],
            catalog);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_SelectedModifierWithoutCatalogFailsBeforeMatching()
    {
        var result = mapper.Map([Modifier("+55 to maximum Life")], catalog: null);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.CatalogRequired,
            Assert.Single(result.Diagnostics).Code);
    }

    private static TradeModifierFilterDraft Modifier(
        string originalText,
        bool isSelected = true,
        ParsedModifierKind kind = ParsedModifierKind.Prefix)
    {
        return new TradeModifierFilterDraft
        {
            OriginalText = originalText,
            ParsedKind = kind,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.test",
            IsSelected = isSelected,
        };
    }

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] entries)
    {
        return new PathOfExileTradeStatCatalog(entries);
    }

    private static PathOfExileTradeStatEntry Entry(
        string id,
        string text,
        string groupId)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = groupId,
            GroupLabel = groupId,
            Id = id,
            Text = text,
            Type = groupId,
        };
    }
}

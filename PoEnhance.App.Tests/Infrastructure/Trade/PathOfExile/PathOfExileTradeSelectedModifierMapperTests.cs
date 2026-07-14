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
            Draft([Modifier("+55 to maximum Life", isSelected: false)]),
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
            Draft([
                Modifier("+55 to maximum Life"),
                Modifier("Adds 10 to 20 Fire Damage"),
            ]),
            catalog);

        Assert.True(result.IsSuccess);
        Assert.Equal(["explicit.stat_life", "explicit.stat_fire"], result.Filters.Select(filter => filter.StatId));
        Assert.Equal([55m], result.Filters[0].ExtractedNumericValues);
        Assert.Equal([10m, 20m], result.Filters[1].ExtractedNumericValues);
        Assert.Equal([0, 1], result.Filters.Select(filter => filter.SourceIndex));
    }

    [Fact]
    public void Map_AdvancedRangeSelectedModifierCreatesOneProviderFilterWithRollValuesOnly()
    {
        var catalog = Catalog(Entry("explicit.stat_cold", "Adds # to # Cold Damage", "explicit"));

        var result = mapper.Map(
            Draft([Modifier("Adds 46(41-55) to 81(81-95) Cold Damage")]),
            catalog);

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_cold", filter.StatId);
        Assert.Equal("Adds # to # Cold Damage", filter.NormalizedItemTemplate);
        Assert.Equal([46m, 81m], filter.ExtractedNumericValues);
        Assert.DoesNotContain(41m, filter.ExtractedNumericValues);
        Assert.DoesNotContain(55m, filter.ExtractedNumericValues);
        Assert.DoesNotContain(95m, filter.ExtractedNumericValues);
    }

    [Fact]
    public void Map_RangerBowFireDamageUsesOfficialLocalStatIdFromDraftContext()
    {
        var catalog = Catalog(
            Entry("explicit.stat_321077055", "Adds # to # Fire Damage", "explicit"),
            Entry("explicit.stat_709508406", "Adds # to # Fire Damage (Local)", "explicit"));

        var result = mapper.Map(
            Draft(
                [
                    Modifier(
                        "Adds 70(63-85) to 139(128-148) Fire Damage",
                        locality: ModifierLocality.Local,
                        statIds: ["local_minimum_added_fire_damage", "local_maximum_added_fire_damage"]),
                ],
                itemClass: "Bows",
                parsedBaseType: "Ranger Bow"),
            catalog);

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_709508406", filter.StatId);
        Assert.Equal("Adds # to # Fire Damage", filter.NormalizedItemTemplate);
        Assert.Equal([70m, 139m], filter.ExtractedNumericValues);
        var trace = Assert.Single(result.Traces);
        Assert.Equal(ModifierLocality.Local, trace.ExpectedLocality);
        Assert.Equal("explicit:Adds # to # Fire Damage", trace.ProviderCandidateGroupKey);
        Assert.Equal("explicit.stat_709508406", trace.SelectedProviderStatId);
        Assert.Equal(["local_maximum_added_fire_damage", "local_minimum_added_fire_damage"], trace.InternalStatIds);
    }

    [Fact]
    public void Map_UnknownLocalityAmbiguityFailsWholeMapping()
    {
        var catalog = Catalog(
            Entry("explicit.global_fire", "Adds # to # Fire Damage", "explicit"),
            Entry("explicit.local_fire", "Adds # to # Fire Damage (Local)", "explicit"));

        var result = mapper.Map(
            Draft([Modifier("Adds 10 to 20 Fire Damage")]),
            catalog);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous, diagnostic.Code);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous, diagnostic.SourceCode);
    }

    [Fact]
    public void Map_UnselectedModifiersAreNotMatchedOrSerialized()
    {
        var catalog = Catalog(Entry("explicit.stat_life", "+# to maximum Life", "explicit"));

        var result = mapper.Map(
            Draft([
                Modifier("+55 to maximum Life", isSelected: false),
                Modifier("+21 to maximum Life"),
            ]),
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

        var result = mapper.Map(Draft([Modifier("+55 to maximum Life")]), catalog);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_AdvancedRangeAmbiguousSelectedModifierFailsWholeMapping()
    {
        var catalog = Catalog(
            Entry("explicit.stat_life_one", "+# to maximum Life", "explicit"),
            Entry("explicit.stat_life_two", "+# to maximum Life", "explicit"));

        var result = mapper.Map(Draft([Modifier("+101(100-114) to maximum Life")]), catalog);

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

        var result = mapper.Map(Draft([Modifier("+55 to maximum Life")]), catalog);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_AdvancedRangeGenuineNotFoundSelectedModifierFailsWholeMapping()
    {
        var catalog = Catalog(Entry("explicit.stat_mana", "+# to maximum Mana", "explicit"));

        var result = mapper.Map(Draft([Modifier("+101(100-114) to maximum Life")]), catalog);

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
            Draft([Modifier(" ")]),
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
            Draft([Modifier("+55 to maximum Life", kind: ParsedModifierKind.Implicit)]),
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
        var result = mapper.Map(Draft([Modifier("+55 to maximum Life")]), catalog: null);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.CatalogRequired,
            Assert.Single(result.Diagnostics).Code);
    }

    private static TradeModifierFilterDraft Modifier(
        string originalText,
        bool isSelected = true,
        ParsedModifierKind kind = ParsedModifierKind.Prefix,
        ModifierLocality locality = ModifierLocality.Unknown,
        IReadOnlyList<string>? statIds = null)
    {
        return new TradeModifierFilterDraft
        {
            OriginalText = originalText,
            ParsedKind = kind,
            Locality = locality,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.test",
            ResolvedStatIds = statIds ?? [],
            IsSelected = isSelected,
        };
    }

    private static TradeSearchDraft Draft(
        IReadOnlyList<TradeModifierFilterDraft> modifiers,
        string itemClass = "Body Armours",
        string parsedBaseType = "Titan Plate")
    {
        return new TradeSearchDraft
        {
            ItemClass = itemClass,
            Rarity = "Rare",
            DisplayName = "Test Item",
            ParsedBaseType = parsedBaseType,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.test",
                ResolvedBaseName = parsedBaseType,
            },
            ModifierFilters = modifiers,
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

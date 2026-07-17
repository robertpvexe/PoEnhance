using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemPropertyResolverTests
{
    [Theory]
    [InlineData(TradeSearchItemPropertyKind.TotalDps, TradeSearchItemPropertyProviderResolutionStatus.Exact, true)]
    [InlineData(TradeSearchItemPropertyKind.PhysicalDps, TradeSearchItemPropertyProviderResolutionStatus.Exact, true)]
    [InlineData(TradeSearchItemPropertyKind.ElementalDps, TradeSearchItemPropertyProviderResolutionStatus.Exact, true)]
    [InlineData(TradeSearchItemPropertyKind.ChaosDps, TradeSearchItemPropertyProviderResolutionStatus.Unsupported, false)]
    [InlineData(TradeSearchItemPropertyKind.AttacksPerSecond, TradeSearchItemPropertyProviderResolutionStatus.Exact, true)]
    [InlineData(TradeSearchItemPropertyKind.CriticalStrikeChance, TradeSearchItemPropertyProviderResolutionStatus.Exact, true)]
    public void Resolve_AllReviewedKindsProduceAuditedVerdict(
        TradeSearchItemPropertyKind kind,
        TradeSearchItemPropertyProviderResolutionStatus expectedStatus,
        bool expectedSearchable)
    {
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft();

        var result = new PathOfExileTradeItemPropertyResolver().Resolve(
            draft,
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());

        var property = Assert.Single(result.ItemProperties, property => property.Kind == kind);
        Assert.Equal(expectedStatus, property.ProviderResolutionStatus);
        Assert.Equal(expectedSearchable, property.IsSearchable);
        Assert.Equal(expectedSearchable, property.NotSearchableReason is null);
    }

    [Fact]
    public void Resolve_MissingOfficialEntryRemainsCatalogMissingAndUnresolved()
    {
        var catalog = WithoutDefinition("dps");
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(TradeSearchItemPropertyKind.TotalDps, 437.45m)]);

        var result = new PathOfExileTradeItemPropertyResolver().Resolve(draft, catalog);

        var property = Assert.Single(result.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Unresolved, property.ProviderResolutionStatus);
        Assert.False(property.IsSearchable);
        Assert.Contains("does not contain", property.NotSearchableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_IncompatibleNumericShapeRemainsUnresolved()
    {
        var official = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
        var definitions = official.NumericFilterDefinitions
            .Select(definition => definition.FilterId == "aps"
                ? definition with { SupportsMinMax = false }
                : definition);
        var catalog = new PathOfExileTradeFilterCatalog(
            official.CategoryOptions,
            numericFilterDefinitions: definitions);
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.AttacksPerSecond,
                1.20m)]);

        var result = new PathOfExileTradeItemPropertyResolver().Resolve(draft, catalog);

        var property = Assert.Single(result.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Unresolved, property.ProviderResolutionStatus);
        Assert.Contains("incompatible", property.NotSearchableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_DuplicateOfficialIdentityIsAmbiguousEvenWhenDefinitionsMatch()
    {
        var official = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
        var dps = Assert.Single(official.NumericFilterDefinitions, definition => definition.FilterId == "dps");
        var catalog = new PathOfExileTradeFilterCatalog(
            official.CategoryOptions,
            numericFilterDefinitions: [.. official.NumericFilterDefinitions, dps with { ProviderOrder = 99 }]);
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(TradeSearchItemPropertyKind.TotalDps, 437.45m)]);

        var result = new PathOfExileTradeItemPropertyResolver().Resolve(draft, catalog);

        var property = Assert.Single(result.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Ambiguous, property.ProviderResolutionStatus);
        Assert.False(property.IsSearchable);
        Assert.Contains("duplicate or conflicting", property.NotSearchableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ManuallyConstructedNonWeaponPropertyCannotBecomeSearchable()
    {
        var property = PathOfExileTradeItemPropertyTestFixtures.Property(
            TradeSearchItemPropertyKind.TotalDps,
            437.45m,
            selected: true);

        var result = new PathOfExileTradeItemPropertyResolver().Resolve(
            PathOfExileTradeItemPropertyTestFixtures.NonWeaponDraft(property),
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());

        property = Assert.Single(result.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Unsupported, property.ProviderResolutionStatus);
        Assert.False(property.IsSearchable);
        Assert.Contains("weapon-property derivation", property.NotSearchableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkCatalogUnavailable_PreservesReviewedUnsupportedChaosVerdict()
    {
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.ChaosDps,
                42m)]);

        var result = new PathOfExileTradeItemPropertyResolver().MarkCatalogUnavailable(
            draft,
            "Catalog unavailable for test.");

        var property = Assert.Single(result.ItemProperties);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Unsupported, property.ProviderResolutionStatus);
        Assert.False(property.IsSearchable);
        Assert.Contains("does not expose a Chaos DPS", property.NotSearchableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void MapSelected_UsesVerifiedIdentityAndCanonicalBoundsOnly()
    {
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.PhysicalDps,
                169.065m,
                selected: true,
                maximum: 250.125m)]);

        var result = new PathOfExileTradeItemPropertyResolver().MapSelected(
            draft,
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal(0, filter.SourceItemPropertyIndex);
        Assert.Equal("weapon_filters", filter.ProviderGroupId);
        Assert.Equal("pdps", filter.ProviderFilterId);
        Assert.Equal(169.065m, filter.RequestedMinimum);
        Assert.Equal(250.125m, filter.RequestedMaximum);
    }

    private static PathOfExileTradeFilterCatalog WithoutDefinition(string filterId)
    {
        var official = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
        return new PathOfExileTradeFilterCatalog(
            official.CategoryOptions,
            numericFilterDefinitions: official.NumericFilterDefinitions.Where(definition =>
                definition.FilterId != filterId));
    }
}

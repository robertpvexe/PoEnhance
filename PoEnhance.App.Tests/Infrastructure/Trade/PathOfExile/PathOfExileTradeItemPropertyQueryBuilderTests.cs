using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemPropertyQueryBuilderTests
{
    private const string League = "Mirage";
    private readonly PathOfExileTradeQueryBuilder builder = new();

    [Theory]
    [InlineData(TradeSearchItemPropertyKind.TotalDps, "dps", "437.45")]
    [InlineData(TradeSearchItemPropertyKind.PhysicalDps, "pdps", "169.065")]
    [InlineData(TradeSearchItemPropertyKind.ElementalDps, "edps", "325")]
    [InlineData(TradeSearchItemPropertyKind.AttacksPerSecond, "aps", "1.20")]
    [InlineData(TradeSearchItemPropertyKind.CriticalStrikeChance, "crit", "5.00")]
    public void Build_PropertyOnlyRequestUsesExactNumericWeaponFilter(
        TradeSearchItemPropertyKind kind,
        string expectedFilterId,
        string expectedRawMinimum)
    {
        var value = decimal.Parse(expectedRawMinimum, System.Globalization.CultureInfo.InvariantCulture);
        var (draft, filters) = ResolvedSelection(kind, value);

        var result = Build(draft, filters);

        var providerValue = WeaponFilter(result, expectedFilterId);
        Assert.Equal(value, providerValue.GetProperty("min").GetDecimal());
        Assert.Equal(expectedRawMinimum, providerValue.GetProperty("min").GetRawText());
        Assert.False(providerValue.TryGetProperty("max", out _));
        Assert.Equal(JsonValueKind.Number, providerValue.GetProperty("min").ValueKind);
        Assert.DoesNotContain("\"damage\"", result.SerializedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RequestedMaximumIsSerializedAndNullMaximumIsOmitted()
    {
        var (boundedDraft, boundedFilters) = ResolvedSelection(
            TradeSearchItemPropertyKind.PhysicalDps,
            169.065m,
            250.125m);
        var (openDraft, openFilters) = ResolvedSelection(
            TradeSearchItemPropertyKind.PhysicalDps,
            169.065m);

        var bounded = WeaponFilter(Build(boundedDraft, boundedFilters), "pdps");
        var open = WeaponFilter(Build(openDraft, openFilters), "pdps");

        Assert.Equal(250.125m, bounded.GetProperty("max").GetDecimal());
        Assert.False(open.TryGetProperty("max", out _));
    }

    [Fact]
    public void Build_CriticalChanceUsesPercentagePointsWithoutDivision()
    {
        var (draft, filters) = ResolvedSelection(
            TradeSearchItemPropertyKind.CriticalStrikeChance,
            5.00m);

        var value = WeaponFilter(Build(draft, filters), "crit");

        Assert.Equal(5.00m, value.GetProperty("min").GetDecimal());
        Assert.NotEqual(0.05m, value.GetProperty("min").GetDecimal());
    }

    [Fact]
    public void Build_SelectedPropertyWithoutMappingFailsInsteadOfSilentlyOmittingIt()
    {
        var (draft, _) = ResolvedSelection(TradeSearchItemPropertyKind.TotalDps, 437.45m);

        var result = builder.Build(draft, Validate(draft), League);

        AssertFailure(
            result,
            PathOfExileTradeQueryDiagnosticCodes.SelectedItemPropertiesMissingProviderMapping);
    }

    [Fact]
    public void Build_DuplicateSourceIndexIsRejected()
    {
        var (draft, filters) = ResolvedSelection(TradeSearchItemPropertyKind.TotalDps, 437.45m);

        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: [filters[0], filters[0]]);

        AssertFailure(
            result,
            PathOfExileTradeQueryDiagnosticCodes.DuplicateSelectedItemPropertySourceIndex);
    }

    [Fact]
    public void Build_MappingForUnselectedPropertyIsRejected()
    {
        var (selectedDraft, filters) = ResolvedSelection(TradeSearchItemPropertyKind.TotalDps, 437.45m);
        var draft = selectedDraft with
        {
            ItemProperties = [selectedDraft.ItemProperties[0] with { IsSelected = false }],
        };

        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: filters);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.SelectedItemPropertyMappingMismatch);
    }

    [Fact]
    public void Build_DuplicateProviderIdentityIsRejected()
    {
        var unresolved = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
        [
            PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.TotalDps,
                437.45m,
                selected: true),
            PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.PhysicalDps,
                169.065m,
                selected: true),
        ]);
        var draft = new PathOfExileTradeItemPropertyResolver().Resolve(
            unresolved,
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());
        var filters =
        new[]
        {
            Filter(0, "dps", 437.45m),
            Filter(1, "dps", 169.065m),
        };

        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: filters);

        AssertFailure(
            result,
            PathOfExileTradeQueryDiagnosticCodes.DuplicateSelectedItemPropertyProviderIdentity);
    }

    [Theory]
    [InlineData("", "dps")]
    [InlineData("weapon_filters", "")]
    [InlineData("weapon_filters", "damage")]
    public void Build_BlankOrPerHitProviderIdentityIsRejected(string groupId, string filterId)
    {
        var (draft, filters) = ResolvedSelection(TradeSearchItemPropertyKind.TotalDps, 437.45m);
        var forged = filters[0] with
        {
            ProviderGroupId = groupId,
            ProviderFilterId = filterId,
        };

        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: [forged]);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping);
    }

    [Fact]
    public void Build_ChangedProviderBoundsAreRejected()
    {
        var (draft, filters) = ResolvedSelection(TradeSearchItemPropertyKind.TotalDps, 437.45m);
        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: [filters[0] with { RequestedMinimum = 437m }]);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping);
    }

    [Fact]
    public void Build_ChaosDpsCannotBeForgedIntoSerialization()
    {
        var unresolved = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.ChaosDps,
                42m,
                selected: true)]);
        var draft = new PathOfExileTradeItemPropertyResolver().Resolve(
            unresolved,
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());

        var result = builder.Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: [Filter(0, "dps", 42m)]);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping);
        Assert.Null(result.SerializedJson);
    }

    [Fact]
    public void Build_WrongReviewedIdentityForPropertyKindIsRejected()
    {
        var (draft, filters) = ResolvedSelection(
            TradeSearchItemPropertyKind.PhysicalDps,
            169.065m);

        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: [filters[0] with { ProviderFilterId = "dps" }]);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping);
    }

    [Fact]
    public void Build_SelectedPropertyRequiresVerifiedCatalogAtSerializationBoundary()
    {
        var (draft, filters) = ResolvedSelection(
            TradeSearchItemPropertyKind.TotalDps,
            437.45m);

        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            selectedItemPropertyFilters: filters);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping);
    }

    [Fact]
    public void Build_ManuallyForgedNonWeaponPropertyCannotEmitWeaponFilters()
    {
        var source = PathOfExileTradeItemPropertyTestFixtures.Property(
            TradeSearchItemPropertyKind.TotalDps,
            437.45m,
            selected: true);
        var draft = PathOfExileTradeItemPropertyTestFixtures.NonWeaponDraft(source with
        {
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
            IsSearchable = true,
            NotSearchableReason = null,
        });

        var result = builder.Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: [Filter(0, "dps", 437.45m)]);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping);
        Assert.Null(result.SerializedJson);
    }

    [Fact]
    public void Build_NoSelectedPropertyLeavesModifierOnlyJsonByteForByteUnchanged()
    {
        var draft = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.TotalDps,
                437.45m)]);

        var before = builder.Build(draft, Validate(draft), League);
        var after = builder.Build(
            draft,
            Validate(draft),
            League,
            selectedItemPropertyFilters: []);

        Assert.True(before.IsSuccess);
        Assert.True(after.IsSuccess);
        Assert.Equal(before.SerializedJson, after.SerializedJson);
        Assert.DoesNotContain("weapon_filters", after.SerializedJson, StringComparison.Ordinal);
    }

    private PathOfExileTradeQueryBuildResult Build(
        TradeSearchDraft draft,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter> filters)
    {
        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: filters);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.SerializedJson);
        return result;
    }

    private static (TradeSearchDraft Draft, IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter> Filters)
        ResolvedSelection(
            TradeSearchItemPropertyKind kind,
            decimal minimum,
            decimal? maximum = null)
    {
        var catalog = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
        var resolver = new PathOfExileTradeItemPropertyResolver();
        var unresolved = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                kind,
                minimum,
                selected: true,
                maximum: maximum)]);
        var draft = resolver.Resolve(unresolved, catalog);
        var mapping = resolver.MapSelected(draft, catalog);
        Assert.True(mapping.IsSuccess);
        return (draft, mapping.Filters);
    }

    private static PathOfExileTradeSelectedItemPropertyFilter Filter(
        int sourceIndex,
        string filterId,
        decimal minimum)
    {
        return new PathOfExileTradeSelectedItemPropertyFilter
        {
            SourceItemPropertyIndex = sourceIndex,
            ProviderGroupId = "weapon_filters",
            ProviderFilterId = filterId,
            RequestedMinimum = minimum,
        };
    }

    private static TradeSearchValidationResult Validate(TradeSearchDraft draft)
    {
        return new TradeSearchDraftValidator().Validate(draft);
    }

    private static PathOfExileTradeFilterCatalog Catalog()
    {
        return PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    }

    private static JsonElement WeaponFilter(
        PathOfExileTradeQueryBuildResult result,
        string filterId)
    {
        using var document = JsonDocument.Parse(result.SerializedJson!);
        return document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("weapon_filters")
            .GetProperty("filters")
            .GetProperty(filterId)
            .Clone();
    }

    private static void AssertFailure(
        PathOfExileTradeQueryBuildResult result,
        string expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Request);
        Assert.Null(result.SerializedJson);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }
}

using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
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

    [Theory]
    [InlineData(TradeSearchItemPropertyKind.EnergyShield, "es")]
    [InlineData(TradeSearchItemPropertyKind.Armour, "ar")]
    [InlineData(TradeSearchItemPropertyKind.EvasionRating, "ev")]
    [InlineData(TradeSearchItemPropertyKind.Ward, "ward")]
    [InlineData(TradeSearchItemPropertyKind.ChanceToBlock, "block")]
    public void Build_DefensivePropertyUsesExactNumericArmourFilter(
        TradeSearchItemPropertyKind kind,
        string filterId)
    {
        var (draft, filters) = ResolvedDefensiveSelection(
            PathOfExileTradeItemPropertyTestFixtures.Property(kind, 123m, selected: true));

        var value = ArmourFilter(Build(draft, filters), filterId);

        Assert.Equal(123m, value.GetProperty("min").GetDecimal());
        Assert.False(value.TryGetProperty("max", out _));
    }

    [Fact]
    public void Build_TwoDefensiveParentsSerializeOnceInSameArmourGroup()
    {
        var armour = PathOfExileTradeItemPropertyTestFixtures.Property(
            TradeSearchItemPropertyKind.Armour, 1000m, selected: true);
        var evasion = PathOfExileTradeItemPropertyTestFixtures.Property(
            TradeSearchItemPropertyKind.EvasionRating, 900m, selected: true);
        var (draft, filters) = ResolvedDefensiveSelection(armour, evasion);

        var result = Build(draft, filters);

        Assert.Equal(1000m, ArmourFilter(result, "ar").GetProperty("min").GetDecimal());
        Assert.Equal(900m, ArmourFilter(result, "ev").GetProperty("min").GetDecimal());
        Assert.Equal(1, result.SerializedJson!.Split("\"ar\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, result.SerializedJson.Split("\"ev\"", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData(TradeSearchItemPropertyKind.EvasionRating, "ev")]
    [InlineData(TradeSearchItemPropertyKind.ChanceToBlock, "block")]
    public void Build_DefensiveParentSerializesRequestedMinimumAndMaximum(
        TradeSearchItemPropertyKind kind,
        string filterId)
    {
        var property = PathOfExileTradeItemPropertyTestFixtures.Property(
            kind,
            123m,
            selected: true,
            maximum: 456m);
        var (draft, filters) = ResolvedDefensiveSelection(property);

        var value = ArmourFilter(Build(draft, filters), filterId);

        Assert.Equal(123m, value.GetProperty("min").GetDecimal());
        Assert.Equal(456m, value.GetProperty("max").GetDecimal());
    }

    [Theory]
    [InlineData(TradeSearchItemPropertyKind.EvasionRating, "ev")]
    [InlineData(TradeSearchItemPropertyKind.ChanceToBlock, "block")]
    public void Build_DefensiveParentAndIndependentChildStatCoexistWithoutDuplication(
        TradeSearchItemPropertyKind kind,
        string filterId)
    {
        var (resolved, filters) = ResolvedDefensiveSelection(
            PathOfExileTradeItemPropertyTestFixtures.Property(kind, 123m, selected: true));
        var draft = resolved with { ModifierFilters = [SelectedModifier(0)] };
        var result = Build(
            draft,
            filters,
            [SelectedModifierFilter(0, "explicit.test_defensive_child")]);

        Assert.Equal(123m, ArmourFilter(result, filterId).GetProperty("min").GetDecimal());
        Assert.Equal(
            "explicit.test_defensive_child",
            Assert.Single(StatFilters(result)).GetProperty("id").GetString());
        Assert.Equal(1, result.SerializedJson!.Split($"\"{filterId}\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            1,
            result.SerializedJson.Split("\"explicit.test_defensive_child\"", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Build_TwoDefensiveParentsAndOneSharedChildStatEachSerializeOnce()
    {
        var (resolved, filters) = ResolvedDefensiveSelection(
            PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.EvasionRating, 900m, selected: true),
            PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.ChanceToBlock, 25m, selected: true));
        var draft = resolved with { ModifierFilters = [SelectedModifier(0), SelectedModifier(1)] };
        var sharedChild = SelectedModifierFilter(0, "explicit.test_shared_defensive_child") with
        {
            SourceIndexes = [0, 1],
        };

        var result = Build(draft, filters, [sharedChild]);

        Assert.Equal(900m, ArmourFilter(result, "ev").GetProperty("min").GetDecimal());
        Assert.Equal(25m, ArmourFilter(result, "block").GetProperty("min").GetDecimal());
        Assert.Equal(
            "explicit.test_shared_defensive_child",
            Assert.Single(StatFilters(result)).GetProperty("id").GetString());
        Assert.Equal(1, result.SerializedJson!.Split("\"ev\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, result.SerializedJson.Split("\"block\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            1,
            result.SerializedJson.Split("\"explicit.test_shared_defensive_child\"", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Build_InactiveDefensiveParentProducesNoItemPropertyFilter()
    {
        var draft = PathOfExileTradeItemPropertyTestFixtures.ArmourDraft(
            PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.EvasionRating,
                900m));

        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            providerFilterCatalog: Catalog(),
            selectedItemPropertyFilters: []);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("armour_filters", result.SerializedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ev\"", result.SerializedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UnselectedIncompatibleBlockParentDoesNotBlockSelectedChildOnlyQuery()
    {
        var official = Catalog();
        var incompatibleCatalog = new PathOfExileTradeFilterCatalog(
            official.CategoryOptions,
            numericFilterDefinitions: official.NumericFilterDefinitions.Select(definition =>
                definition.FilterId == "block"
                    ? definition with { SupportsMinMax = false }
                    : definition));
        var unresolved = PathOfExileTradeItemPropertyTestFixtures.ArmourDraft(
            PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.ChanceToBlock,
                25m));
        var resolved = new PathOfExileTradeItemPropertyResolver().Resolve(unresolved, incompatibleCatalog);
        var draft = resolved with { ModifierFilters = [SelectedModifier(0)] };

        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            [SelectedModifierFilter(0, "explicit.test_defensive_child")],
            providerFilterCatalog: incompatibleCatalog,
            selectedItemPropertyFilters: []);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "explicit.test_defensive_child",
            Assert.Single(StatFilters(result)).GetProperty("id").GetString());
        Assert.DoesNotContain("armour_filters", result.SerializedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"block\"", result.SerializedJson, StringComparison.Ordinal);
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
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter> filters,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters = null)
    {
        var result = builder.Build(
            draft,
            Validate(draft),
            League,
            selectedModifierFilters,
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

    private static ResolvedSearchComponent SelectedModifier(int sourceComponentIndex)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = $"modifier:{sourceComponentIndex}:0",
            SourceComponentIndex = sourceComponentIndex,
            OriginalText = "20% increased Evasion Rating",
            CanonicalSignature = "<number>% increased Evasion Rating",
            ParsedKind = ParsedModifierKind.Prefix,
            Locality = ModifierLocality.Local,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = $"test.mod.{sourceComponentIndex}",
            ResolvedStatIds = ["local_evasion_rating_+%"],
            IsSearchable = true,
            IsSelected = true,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = "explicit.test_defensive_child",
        };
    }

    private static PathOfExileTradeSelectedModifierFilter SelectedModifierFilter(
        int sourceIndex,
        string statId)
    {
        return new PathOfExileTradeSelectedModifierFilter
        {
            SourceIndex = sourceIndex,
            StatId = statId,
            OriginalText = "20% increased Evasion Rating",
            NormalizedItemTemplate = "#% increased Evasion Rating",
        };
    }

    private static (TradeSearchDraft Draft, IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter> Filters)
        ResolvedDefensiveSelection(params TradeSearchItemProperty[] properties)
    {
        var catalog = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
        var resolver = new PathOfExileTradeItemPropertyResolver();
        var draft = resolver.Resolve(PathOfExileTradeItemPropertyTestFixtures.ArmourDraft(properties), catalog);
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

    private static JsonElement ArmourFilter(
        PathOfExileTradeQueryBuildResult result,
        string filterId)
    {
        using var document = JsonDocument.Parse(result.SerializedJson!);
        return document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("armour_filters")
            .GetProperty("filters")
            .GetProperty(filterId)
            .Clone();
    }

    private static JsonElement[] StatFilters(PathOfExileTradeQueryBuildResult result)
    {
        using var document = JsonDocument.Parse(result.SerializedJson!);
        return document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .Select(filter => filter.Clone())
            .ToArray();
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

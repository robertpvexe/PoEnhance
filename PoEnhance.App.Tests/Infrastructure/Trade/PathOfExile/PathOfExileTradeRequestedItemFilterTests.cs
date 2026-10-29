using System.Collections.Immutable;
using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeRequestedItemFilterTests
{
    private const string League = "Mirage";

    [Fact]
    public void OfficialShapedCatalog_RetainsReviewedRequestedNumericDefinitions()
    {
        var catalog = RequestedCatalog();

        Assert.Equal(
            ["ilvl", "quality", "sockets", "links"],
            catalog.NumericFilterDefinitions
                .Where(definition => definition.GroupId is "misc_filters" or "socket_filters")
                .Select(definition => definition.FilterId));
        Assert.All(
            catalog.NumericFilterDefinitions.Where(definition =>
                definition.FilterId is "ilvl" or "quality" or "links" or "sockets"),
            definition => Assert.True(definition.SupportsMinMax));
        Assert.All(
            catalog.NumericFilterDefinitions.Where(definition =>
                definition.FilterId is "links" or "sockets"),
            definition => Assert.True(definition.SupportsSockets));
    }

    [Theory]
    [InlineData(TradeSearchRequestedItemFilterKind.ItemLevel, "misc_filters", "ilvl", 75)]
    [InlineData(TradeSearchRequestedItemFilterKind.Quality, "misc_filters", "quality", 5)]
    [InlineData(TradeSearchRequestedItemFilterKind.Links, "socket_filters", "links", 3)]
    [InlineData(TradeSearchRequestedItemFilterKind.Sockets, "socket_filters", "sockets", 6)]
    public void Build_OneActiveRequestedFilterSerializesMinimumWithoutMaximum(
        TradeSearchRequestedItemFilterKind kind,
        string groupId,
        string filterId,
        int minimumValue)
    {
        var catalog = RequestedCatalog();
        var draft = ResolveRequested(
            WithRequestedFilters(PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(),
                (kind, minimumValue)),
            catalog);

        var result = Build(draft, catalog);

        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var value = RequestedValue(document, groupId, filterId);
        AssertMinimumOnly(value, minimumValue);
    }

    [Theory]
    [InlineData(TradeSearchRequestedItemFilterKind.ItemLevel, "misc_filters", "ilvl")]
    [InlineData(TradeSearchRequestedItemFilterKind.Quality, "misc_filters", "quality")]
    [InlineData(TradeSearchRequestedItemFilterKind.Links, "socket_filters", "links")]
    [InlineData(TradeSearchRequestedItemFilterKind.Sockets, "socket_filters", "sockets")]
    public void Build_ActiveEmptyRequestedFilterIsOmittedWithoutBlocking(
        TradeSearchRequestedItemFilterKind kind,
        string groupId,
        string filterId)
    {
        var catalog = RequestedCatalog();
        var source = WithRequestedFilters(PathOfExileTradeItemPropertyTestFixtures.WeaponDraft());
        var draft = ResolveRequested(source with
        {
            RequestedItemFilters = source.RequestedItemFilters
                .Select(filter => filter.Kind == kind
                    ? filter with
                    {
                        IsActive = true,
                        CurrentText = "   ",
                        RequestedMinimum = null,
                        LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Empty,
                        DiagnosticReason = null,
                    }
                    : filter)
                .ToImmutableArray(),
        }, catalog);

        var validation = new TradeSearchDraftValidator().Validate(draft);
        var mapping = new PathOfExileTradeRequestedItemFilterResolver().MapSelected(draft, catalog);
        var result = new PathOfExileTradeQueryBuilder().Build(
            draft,
            validation,
            League,
            selectedModifierFilters: [],
            providerItemIdentity: null,
            providerFilterCatalog: catalog,
            selectedItemPropertyFilters: [],
            selectedRequestedItemFilters: mapping.Filters);

        Assert.True(validation.IsValid);
        Assert.True(mapping.IsSuccess);
        Assert.Empty(mapping.Filters);
        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var filters = document.RootElement.GetProperty("query").GetProperty("filters");
        Assert.False(filters.TryGetProperty(groupId, out var group) &&
            group.GetProperty("filters").TryGetProperty(filterId, out _));
    }

    [Fact]
    public void Build_AllRequestedFiltersCoexistWithCategoryAndPhysicalDpsWithoutSocketColours()
    {
        var catalog = RequestedCatalog();
        var source = PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(
            [PathOfExileTradeItemPropertyTestFixtures.Property(
                TradeSearchItemPropertyKind.PhysicalDps,
                202.725m,
                selected: true)],
            categoryMode: true);
        var propertyResolver = new PathOfExileTradeItemPropertyResolver();
        var draft = propertyResolver.Resolve(
            ResolveRequested(
                WithRequestedFilters(source,
                    (TradeSearchRequestedItemFilterKind.ItemLevel, 85),
                    (TradeSearchRequestedItemFilterKind.Quality, 28),
                    (TradeSearchRequestedItemFilterKind.Links, 3),
                    (TradeSearchRequestedItemFilterKind.Sockets, 5)) with
                {
                    SocketText = "G-R-R W-B",
                },
                catalog),
            catalog);
        draft = draft with
        {
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Mirrored = TradeTriState.No,
                Corrupted = TradeTriState.No,
                Identified = TradeTriState.Yes,
            },
        };
        var selectedProperty = propertyResolver.MapSelected(draft, catalog);
        Assert.True(selectedProperty.IsSuccess);

        var result = Build(draft, catalog, selectedItemProperties: selectedProperty.Filters);

        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        var filters = query.GetProperty("filters");
        Assert.False(query.TryGetProperty("type", out _));
        Assert.Equal("weapon.oneaxe", filters
            .GetProperty("type_filters").GetProperty("filters")
            .GetProperty("category").GetProperty("option").GetString());
        Assert.Equal(202.725m, filters
            .GetProperty("weapon_filters").GetProperty("filters")
            .GetProperty("pdps").GetProperty("min").GetDecimal());
        AssertMinimumOnly(document, "misc_filters", "ilvl", 85);
        AssertMinimumOnly(document, "misc_filters", "quality", 28);
        AssertMinimumOnly(document, "socket_filters", "links", 3);
        AssertMinimumOnly(document, "socket_filters", "sockets", 5);
        Assert.Equal("false", filters.GetProperty("misc_filters").GetProperty("filters")
            .GetProperty("mirrored").GetProperty("option").GetString());
        Assert.Equal("false", filters.GetProperty("misc_filters").GetProperty("filters")
            .GetProperty("corrupted").GetProperty("option").GetString());
        Assert.Equal("true", filters.GetProperty("misc_filters").GetProperty("filters")
            .GetProperty("identified").GetProperty("option").GetString());
        Assert.DoesNotContain("G-R-R", result.SerializedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("socket_count", result.SerializedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("socket_colors", result.SerializedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ActiveQualityAndItemLevelCoexistWithStatesAndEldritchInfluencesInMiscFilters()
    {
        var catalog = RequestedCatalogWithEldritch();
        var draft = ResolveRequested(
            WithRequestedFilters(
                PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(),
                (TradeSearchRequestedItemFilterKind.ItemLevel, 84),
                (TradeSearchRequestedItemFilterKind.Quality, 28)),
            catalog);
        draft = draft with
        {
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Mirrored = TradeTriState.No,
                Corrupted = TradeTriState.Yes,
                Identified = TradeTriState.Yes,
                Fractured = TradeTriState.No,
            },
            InfluenceCriteria = TradeSearchInfluenceCriteriaFactory.Create(
                [],
                ["Searing Exarch Item", "Eater of Worlds Item"]),
        };
        var influence = new PathOfExileTradeInfluenceFilterResolver()
            .MapSelected(draft, statCatalog: null, catalog);
        Assert.True(influence.IsSuccess);
        var requested = new PathOfExileTradeRequestedItemFilterResolver().MapSelected(draft, catalog);
        Assert.True(requested.IsSuccess);

        var result = new PathOfExileTradeQueryBuilder().Build(
            draft,
            new TradeSearchDraftValidator().Validate(draft),
            League,
            selectedModifierFilters: [],
            providerItemIdentity: null,
            providerFilterCatalog: catalog,
            selectedItemPropertyFilters: [],
            selectedRequestedItemFilters: requested.Filters,
            selectedInfluenceFilters: influence.Filters);

        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var misc = document.RootElement.GetProperty("query").GetProperty("filters")
            .GetProperty("misc_filters").GetProperty("filters");
        AssertMinimumOnly(misc.GetProperty("ilvl"), 84);
        AssertMinimumOnly(misc.GetProperty("quality"), 28);
        Assert.Equal("false", misc.GetProperty("mirrored").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("corrupted").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("identified").GetProperty("option").GetString());
        Assert.Equal("false", misc.GetProperty("fractured_item").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("searing_item").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("tangled_item").GetProperty("option").GetString());
        Assert.Equal(8, misc.EnumerateObject().Count());
    }

    [Fact]
    public void Build_InactiveQualityAndItemLevelOmitMiscCriteriaEvenWhenPrefillsExist()
    {
        var catalog = RequestedCatalog();
        var source = WithRequestedFilters(PathOfExileTradeItemPropertyTestFixtures.WeaponDraft());
        var draft = ResolveRequested(source with
        {
            RequestedItemFilters = source.RequestedItemFilters
                .Select(filter => filter.Kind is TradeSearchRequestedItemFilterKind.ItemLevel
                        or TradeSearchRequestedItemFilterKind.Quality
                    ? filter with
                    {
                        IsActive = false,
                        CurrentText = filter.Kind == TradeSearchRequestedItemFilterKind.ItemLevel ? "84" : "28",
                        RequestedMinimum = filter.Kind == TradeSearchRequestedItemFilterKind.ItemLevel ? 84 : 28,
                        LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    }
                    : filter)
                .ToImmutableArray(),
        }, catalog);

        var result = Build(draft, catalog);

        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var filters = document.RootElement.GetProperty("query").GetProperty("filters");
        Assert.False(filters.TryGetProperty("misc_filters", out _));
    }

    [Fact]
    public void Build_ExactBaseWithIndependentActiveQualityAndItemLevelSerializesAllThree()
    {
        var catalog = RequestedCatalog();
        var draft = ResolveRequested(
            WithRequestedFilters(
                PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(),
                (TradeSearchRequestedItemFilterKind.ItemLevel, 84),
                (TradeSearchRequestedItemFilterKind.Quality, 28)),
            catalog);

        var result = Build(draft, catalog);

        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("Reaver Axe", query.GetProperty("type").GetString());
        AssertMinimumOnly(document, "misc_filters", "ilvl", 84);
        AssertMinimumOnly(document, "misc_filters", "quality", 28);

        var inactiveQuality = ResolveRequested(draft with
        {
            RequestedItemFilters = draft.RequestedItemFilters
                .Select(filter => filter.Kind == TradeSearchRequestedItemFilterKind.Quality
                    ? filter with { IsActive = false }
                    : filter)
                .ToImmutableArray(),
        }, catalog);
        var withoutQuality = Build(inactiveQuality, catalog);
        Assert.True(withoutQuality.IsSuccess, SingleOrDefault(withoutQuality.Diagnostics)?.Message);
        using var withoutQualityDocument = JsonDocument.Parse(withoutQuality.SerializedJson!);
        Assert.Equal("Reaver Axe", withoutQualityDocument.RootElement.GetProperty("query").GetProperty("type").GetString());
        AssertMinimumOnly(withoutQualityDocument, "misc_filters", "ilvl", 84);
        Assert.False(withoutQualityDocument.RootElement.GetProperty("query").GetProperty("filters")
            .GetProperty("misc_filters").GetProperty("filters").TryGetProperty("quality", out _));
    }

    [Fact]
    public void Build_AllRequestedFiltersCoexistWithSelectedModifierStatGroup()
    {
        var catalog = RequestedCatalog();
        var component = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "27% increased Attack Speed",
            CanonicalSignature = "<number>% increased Attack Speed",
            ParsedKind = ParsedModifierKind.Suffix,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "modifier.attack-speed",
            ResolvedStatIds = ["local_attack_speed_+%"],
            IsSearchable = true,
            IsSelected = true,
        };
        var draft = ResolveRequested(
            WithRequestedFilters(
                PathOfExileTradeItemPropertyTestFixtures.WeaponDraft() with
                {
                    ModifierFilters = [component],
                },
                (TradeSearchRequestedItemFilterKind.ItemLevel, 85),
                (TradeSearchRequestedItemFilterKind.Quality, 20),
                (TradeSearchRequestedItemFilterKind.Links, 3),
                (TradeSearchRequestedItemFilterKind.Sockets, 5)),
            catalog);
        draft = draft with
        {
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Mirrored = TradeTriState.No,
                Corrupted = TradeTriState.Yes,
                Identified = TradeTriState.Yes,
            },
        };
        var selectedModifier = new PathOfExileTradeSelectedModifierFilter
        {
            SourceIndex = 0,
            StatId = "explicit.stat_681332047",
            OriginalText = component.OriginalText,
            NormalizedItemTemplate = "#% increased Attack Speed",
            Minimum = 27m,
        };

        var result = Build(draft, catalog, selectedModifiers: [selectedModifier]);

        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var stats = document.RootElement.GetProperty("query").GetProperty("stats")[0]
            .GetProperty("filters");
        Assert.Equal("explicit.stat_681332047", stats[0].GetProperty("id").GetString());
        Assert.Equal(27m, stats[0].GetProperty("value").GetProperty("min").GetDecimal());
        AssertMinimumOnly(document, "misc_filters", "ilvl", 85);
        AssertMinimumOnly(document, "misc_filters", "quality", 20);
        AssertMinimumOnly(document, "socket_filters", "links", 3);
        AssertMinimumOnly(document, "socket_filters", "sockets", 5);
        var misc = document.RootElement.GetProperty("query").GetProperty("filters")
            .GetProperty("misc_filters").GetProperty("filters");
        Assert.Equal("false", misc.GetProperty("mirrored").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("corrupted").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("identified").GetProperty("option").GetString());
    }

    [Fact]
    public void Build_ActiveRequestedFiltersRequireExactOneToOneMappingCoverage()
    {
        var catalog = RequestedCatalog();
        var draft = ResolveRequested(
            WithRequestedFilters(PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(),
                (TradeSearchRequestedItemFilterKind.ItemLevel, 85),
                (TradeSearchRequestedItemFilterKind.Quality, 20)),
            catalog);
        var mappings = new PathOfExileTradeRequestedItemFilterResolver().MapSelected(draft, catalog);
        Assert.True(mappings.IsSuccess);

        var result = new PathOfExileTradeQueryBuilder().Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            League,
            selectedModifierFilters: [],
            providerItemIdentity: null,
            providerFilterCatalog: catalog,
            selectedItemPropertyFilters: [],
            selectedRequestedItemFilters: [mappings.Filters[0]]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PathOfExileTradeQueryDiagnosticCodes.SelectedRequestedItemFilterMappingMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Build_ActiveSocketAndLinkColourConstraintsSerializeIntoDistinctOfficialFields()
    {
        var catalog = RequestedCatalog();
        var source = WithRequestedFilters(PathOfExileTradeItemPropertyTestFixtures.WeaponDraft());
        var draft = ResolveRequested(source with
        {
            RequestedItemFilters = source.RequestedItemFilters
                .Select(filter => filter.Kind switch
                {
                    TradeSearchRequestedItemFilterKind.Sockets => filter with
                    {
                        IsActive = true,
                        CurrentText = "6",
                        RequestedMinimum = 6,
                        RedText = "2",
                        RequestedRed = 2,
                        GreenText = "1",
                        RequestedGreen = 1,
                        BlueText = string.Empty,
                        RequestedBlue = null,
                        WhiteText = "1",
                        RequestedWhite = 1,
                        LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    },
                    TradeSearchRequestedItemFilterKind.Links => filter with
                    {
                        IsActive = true,
                        CurrentText = string.Empty,
                        RequestedMinimum = null,
                        RedText = string.Empty,
                        RequestedRed = null,
                        GreenText = "3",
                        RequestedGreen = 3,
                        BlueText = "3",
                        RequestedBlue = 3,
                        WhiteText = string.Empty,
                        RequestedWhite = null,
                        LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    },
                    _ => filter,
                })
                .ToImmutableArray(),
        }, catalog);

        var result = Build(draft, catalog);

        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var socketFilters = document.RootElement.GetProperty("query").GetProperty("filters")
            .GetProperty("socket_filters").GetProperty("filters");
        var sockets = socketFilters.GetProperty("sockets");
        Assert.Equal(6, sockets.GetProperty("min").GetInt32());
        Assert.Equal(2, sockets.GetProperty("r").GetInt32());
        Assert.Equal(1, sockets.GetProperty("g").GetInt32());
        Assert.False(sockets.TryGetProperty("b", out _));
        Assert.Equal(1, sockets.GetProperty("w").GetInt32());
        Assert.False(sockets.TryGetProperty("max", out _));

        var links = socketFilters.GetProperty("links");
        Assert.False(links.TryGetProperty("min", out _));
        Assert.False(links.TryGetProperty("r", out _));
        Assert.Equal(3, links.GetProperty("g").GetInt32());
        Assert.Equal(3, links.GetProperty("b").GetInt32());
        Assert.False(links.TryGetProperty("w", out _));
        Assert.DoesNotContain("socket_count", result.SerializedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("socket_colors", result.SerializedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_InactiveSocketRowOmitsWholeFilterEvenWhenColourTextIsRetained()
    {
        var catalog = RequestedCatalog();
        var source = WithRequestedFilters(PathOfExileTradeItemPropertyTestFixtures.WeaponDraft());
        var draft = ResolveRequested(source with
        {
            RequestedItemFilters = source.RequestedItemFilters
                .Select(filter => filter.Kind == TradeSearchRequestedItemFilterKind.Sockets
                    ? filter with
                    {
                        IsActive = false,
                        CurrentText = "6",
                        RequestedMinimum = 6,
                        RedText = "2",
                        RequestedRed = 2,
                        LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                    }
                    : filter.Kind == TradeSearchRequestedItemFilterKind.Links
                        ? filter with
                        {
                            IsActive = true,
                            CurrentText = "4",
                            RequestedMinimum = 4,
                            LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                        }
                        : filter)
                .ToImmutableArray(),
        }, catalog);

        var result = Build(draft, catalog);

        Assert.True(result.IsSuccess, SingleOrDefault(result.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var socketFilters = document.RootElement.GetProperty("query").GetProperty("filters")
            .GetProperty("socket_filters").GetProperty("filters");
        Assert.False(socketFilters.TryGetProperty("sockets", out _));
        AssertMinimumOnly(socketFilters.GetProperty("links"), 4);
    }

    [Fact]
    public void Resolve_OutOfRangeActiveFilterBlocksWhileInactiveUnsupportedDoesNot()
    {
        var catalog = RequestedCatalog();
        var active = ResolveRequested(
            WithRequestedFilters(PathOfExileTradeItemPropertyTestFixtures.WeaponDraft(),
                (TradeSearchRequestedItemFilterKind.Quality, 31)),
            catalog);
        var unsupported = Assert.Single(active.RequestedItemFilters, filter =>
            filter.Kind == TradeSearchRequestedItemFilterKind.Quality);
        var inactive = active with
        {
            RequestedItemFilters = active.RequestedItemFilters
                .Select(filter => filter.Kind == TradeSearchRequestedItemFilterKind.Quality
                    ? filter with { IsActive = false }
                    : filter)
                .ToImmutableArray(),
        };

        var activeValidation = new TradeSearchDraftValidator().Validate(active);
        var inactiveValidation = new TradeSearchDraftValidator().Validate(inactive);
        var inactiveBuild = Build(inactive, catalog);

        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
            unsupported.ProviderResolutionStatus);
        Assert.Contains(activeValidation.Diagnostics, diagnostic => diagnostic.Code ==
            TradeSearchValidationDiagnosticCodes.RequestedItemFilterUnsupported);
        Assert.DoesNotContain(inactiveValidation.Diagnostics, diagnostic =>
            diagnostic.Severity == TradeSearchValidationSeverity.Error);
        Assert.True(inactiveBuild.IsSuccess, SingleOrDefault(inactiveBuild.Diagnostics)?.Message);
        using var document = JsonDocument.Parse(inactiveBuild.SerializedJson!);
        var filters = document.RootElement.GetProperty("query").GetProperty("filters");
        Assert.False(filters.TryGetProperty("misc_filters", out _));
        Assert.False(filters.TryGetProperty("socket_filters", out _));
    }

    private static TradeSearchDraft WithRequestedFilters(
        TradeSearchDraft draft,
        params (TradeSearchRequestedItemFilterKind Kind, int Value)[] active)
    {
        var activeByKind = active.ToDictionary(pair => pair.Kind, pair => pair.Value);
        return draft with
        {
            RequestedItemFilters = Enum.GetValues<TradeSearchRequestedItemFilterKind>()
                .Select(kind => new TradeSearchRequestedItemFilter
                {
                    Kind = kind,
                    Label = kind switch
                    {
                        TradeSearchRequestedItemFilterKind.ItemLevel => "Item Level",
                        TradeSearchRequestedItemFilterKind.Quality => "Quality",
                        TradeSearchRequestedItemFilterKind.Links => "Links",
                        TradeSearchRequestedItemFilterKind.Sockets => "Sockets",
                        _ => kind.ToString(),
                    },
                    ObservedValue = kind == TradeSearchRequestedItemFilterKind.ItemLevel ? 84 : 0,
                    CurrentText = activeByKind.TryGetValue(kind, out var requested)
                        ? requested.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : kind == TradeSearchRequestedItemFilterKind.ItemLevel ? "84" : "0",
                    RequestedMinimum = activeByKind.TryGetValue(kind, out requested)
                        ? requested
                        : kind == TradeSearchRequestedItemFilterKind.ItemLevel ? 84 : 0,
                    IsActive = activeByKind.ContainsKey(kind),
                    LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
                })
                .ToImmutableArray(),
        };
    }

    private static TradeSearchDraft ResolveRequested(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog catalog) =>
        new PathOfExileTradeRequestedItemFilterResolver().Resolve(draft, catalog);

    private static PathOfExileTradeQueryBuildResult Build(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog catalog,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifiers = null,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? selectedItemProperties = null)
    {
        var requested = new PathOfExileTradeRequestedItemFilterResolver().MapSelected(draft, catalog);
        Assert.True(requested.IsSuccess, string.Join(" | ", requested.Diagnostics));
        return new PathOfExileTradeQueryBuilder().Build(
            draft,
            new TradeSearchDraftValidator().Validate(draft),
            League,
            selectedModifiers ?? [],
            providerItemIdentity: null,
            providerFilterCatalog: catalog,
            selectedItemProperties ?? [],
            requested.Filters);
    }

    private static PathOfExileTradeFilterCatalog RequestedCatalog() =>
        RequestedCatalogFromJson(includeEldritch: false);

    private static PathOfExileTradeFilterCatalog RequestedCatalogWithEldritch() =>
        RequestedCatalogFromJson(includeEldritch: true);

    private static PathOfExileTradeFilterCatalog RequestedCatalogFromJson(bool includeEldritch)
    {
        var extraMiscFilters = includeEldritch
            ? """
                ,
                    { "id": "fractured_item", "text": "Fractured Item", "option": { "options": [
                      { "id": null, "text": "Any" },
                      { "id": "true", "text": "Yes" },
                      { "id": "false", "text": "No" }
                    ] } },
                    { "id": "searing_item", "text": "Searing Exarch Item", "option": { "options": [
                      { "id": null, "text": "Any" },
                      { "id": "true", "text": "Yes" },
                      { "id": "false", "text": "No" }
                    ] } },
                    { "id": "tangled_item", "text": "Eater of Worlds Item", "option": { "options": [
                      { "id": null, "text": "Any" },
                      { "id": "true", "text": "Yes" },
                      { "id": "false", "text": "No" }
                    ] } }
              """
            : string.Empty;
        var json = $$"""
            {
              "result": [
                {
                  "id": "type_filters",
                  "title": "Type Filters",
                  "filters": [
                    {
                      "id": "category",
                      "text": "Item Category",
                      "option": { "options": [
                        { "id": "weapon.oneaxe", "text": "One-Handed Axe" }
                      ] }
                    }
                  ]
                },
                {
                  "id": "weapon_filters",
                  "title": "Weapon Filters",
                  "hidden": true,
                  "filters": [
                    { "id": "pdps", "text": "Physical DPS", "minMax": true }
                  ]
                },
                {
                  "id": "misc_filters",
                  "title": "Miscellaneous",
                  "filters": [
                    { "id": "ilvl", "text": "Item Level", "minMax": true },
                    { "id": "quality", "text": "Quality", "minMax": true },
                    { "id": "identified", "text": "Identified", "option": { "options": [
                      { "id": null, "text": "Any" },
                      { "id": "true", "text": "Yes" },
                      { "id": "false", "text": "No" }
                    ] } },
                    { "id": "corrupted", "text": "Corrupted", "option": { "options": [
                      { "id": null, "text": "Any" },
                      { "id": "true", "text": "Yes" },
                      { "id": "false", "text": "No" }
                    ] } },
                    { "id": "mirrored", "text": "Mirrored", "option": { "options": [
                      { "id": null, "text": "Any" },
                      { "id": "true", "text": "Yes" },
                      { "id": "false", "text": "No" }
                    ] } }{{extraMiscFilters}}
                  ]
                },
                {
                  "id": "socket_filters",
                  "title": "Socket Filters",
                  "filters": [
                    { "id": "sockets", "text": "Sockets", "fullSpan": true, "sockets": true, "minMax": true },
                    { "id": "links", "text": "Link Groups", "fullSpan": true, "sockets": true, "minMax": true }
                  ]
                }
              ]
            }
            """;
        var result = new PathOfExileTradeFiltersResponseParser().ParseFiltersResponse(json);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<PathOfExileTradeFilterCatalog>(result.Catalog);
    }

    private static JsonElement RequestedValue(JsonDocument document, string groupId, string filterId) =>
        document.RootElement.GetProperty("query").GetProperty("filters")
            .GetProperty(groupId).GetProperty("filters").GetProperty(filterId);

    private static void AssertMinimumOnly(JsonDocument document, string groupId, string filterId, int value)
    {
        var element = RequestedValue(document, groupId, filterId);
        AssertMinimumOnly(element, value);
    }

    private static void AssertMinimumOnly(JsonElement element, int value)
    {
        Assert.Equal(value, element.GetProperty("min").GetInt32());
        Assert.False(element.TryGetProperty("max", out _));
    }

    private static T? SingleOrDefault<T>(IReadOnlyList<T> values) where T : class =>
        values.Count == 1 ? values[0] : null;
}

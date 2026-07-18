using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeQueryBuilderTests
{
    private const string League = "Mercenaries";

    private readonly PathOfExileTradeQueryBuilder builder = new();

    [Fact]
    public void Build_RareItem_ProducesTypeButNoName()
    {
        var result = BuildSuccessful(Draft(rarity: "Rare", displayName: "Dusk Shell"));

        Assert.Equal("Titan Plate", result.Request?.Query.Type);
        Assert.Null(result.Request?.Query.Name);
        AssertRarityFilter(result.SerializedJson!, "rare");
    }

    [Fact]
    public void Build_MagicItem_ProducesTypeButNoRandomName()
    {
        var result = BuildSuccessful(Draft(
            rarity: "Magic",
            displayName: "Humming Titan Plate"));

        Assert.Equal("Titan Plate", result.Request?.Query.Type);
        Assert.Null(result.Request?.Query.Name);
        AssertRarityFilter(result.SerializedJson!, "magic");
    }

    [Fact]
    public void Build_NormalItem_ProducesType()
    {
        var result = BuildSuccessful(Draft(
            rarity: "Normal",
            displayName: "Titan Plate"));

        Assert.Equal("Titan Plate", result.Request?.Query.Type);
        Assert.Null(result.Request?.Query.Name);
        AssertRarityFilter(result.SerializedJson!, "normal");
    }

    [Fact]
    public void Build_UniqueItem_ProducesNameAndType()
    {
        var result = BuildSuccessful(Draft(
            rarity: "Unique",
            displayName: "Mageblood",
            parsedBaseType: "Heavy Belt",
            resolvedBaseName: "Heavy Belt",
            itemClass: "Belts"));

        Assert.Equal("Mageblood", result.Request?.Query.Name);
        Assert.Equal("Heavy Belt", result.Request?.Query.Type);
    }

    [Fact]
    public void Build_UniqueClusterJewelVoices_ProducesCanonicalUniqueNameAndBaseType()
    {
        var result = BuildSuccessful(Draft(
            rarity: "Unique",
            displayName: "Voices",
            parsedBaseType: "Large Cluster Jewel",
            resolvedBaseName: "Large Cluster Jewel",
            itemClass: "Cluster Jewels"));

        Assert.Equal("Voices", result.Request?.Query.Name);
        Assert.Equal("Large Cluster Jewel", result.Request?.Query.Type);
    }

    [Fact]
    public void Build_OrdinaryMoonbendersWing_ProducesProviderAcceptedNameAndBaseType()
    {
        var result = BuildSuccessful(Draft(
            rarity: "Unique",
            displayName: "Moonbender's Wing",
            parsedBaseType: "Tomahawk",
            resolvedBaseName: "Tomahawk",
            itemClass: "One Hand Axes"));

        Assert.Equal("Moonbender's Wing", result.Request?.Query.Name);
        Assert.Equal("Tomahawk", result.Request?.Query.Type);
    }

    [Fact]
    public void Build_UniqueItemWithoutProviderIdentityFails()
    {
        var result = builder.Build(
            Draft(
                rarity: "Unique",
                displayName: "Moonbender's Wing",
                parsedBaseType: "Tomahawk",
                resolvedBaseName: "Tomahawk",
                itemClass: "One Hand Axes"),
            ValidValidation(),
            League);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.MissingProviderUniqueIdentity);
        Assert.Null(result.SerializedJson);
    }

    [Fact]
    public void Build_FoulbornUniqueUsesCanonicalOrdinaryNameAndProviderVariantFilter()
    {
        var result = BuildSuccessful(
            Draft(
                rarity: "Unique",
                displayName: "Foulborn Moonbender's Wing",
                parsedBaseType: "Tomahawk",
                resolvedBaseName: "Tomahawk",
                itemClass: "One Hand Axes"),
            providerItemIdentity: new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Moonbender's Wing",
                CanonicalType = "Tomahawk",
                Foulborn = TradeTriState.Yes,
            });

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("Moonbender's Wing", query.GetProperty("name").GetString());
        Assert.Equal("Tomahawk", query.GetProperty("type").GetString());
        Assert.Equal("true", query
            .GetProperty("filters")
            .GetProperty("misc_filters")
            .GetProperty("filters")
            .GetProperty("mutated")
            .GetProperty("option")
            .GetString());
    }

    [Theory]
    [InlineData(TradeTriState.Any, false, null)]
    [InlineData(TradeTriState.Yes, true, "true")]
    [InlineData(TradeTriState.No, true, "false")]
    public void Build_FoulbornTriStateControlsProviderFilter(
        TradeTriState foulborn,
        bool expectedFilter,
        string? expectedOption)
    {
        var result = BuildSuccessful(
            Draft(
                rarity: "Unique",
                displayName: "Moonbender's Wing",
                parsedBaseType: "Tomahawk",
                resolvedBaseName: "Tomahawk",
                itemClass: "One Hand Axes"),
            providerItemIdentity: new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Moonbender's Wing",
                CanonicalType = "Tomahawk",
                Foulborn = foulborn,
            });

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var filters = document.RootElement.GetProperty("query").GetProperty("filters");
        if (!expectedFilter)
        {
            Assert.Empty(filters.EnumerateObject());
            return;
        }

        Assert.Equal(expectedOption, filters
            .GetProperty("misc_filters")
            .GetProperty("filters")
            .GetProperty("mutated")
            .GetProperty("option")
            .GetString());
    }

    [Theory]
    [InlineData(TradeItemStateKind.Mirrored, TradeTriState.Yes, "mirrored", "true")]
    [InlineData(TradeItemStateKind.Mirrored, TradeTriState.No, "mirrored", "false")]
    [InlineData(TradeItemStateKind.Corrupted, TradeTriState.Yes, "corrupted", "true")]
    [InlineData(TradeItemStateKind.Corrupted, TradeTriState.No, "corrupted", "false")]
    [InlineData(TradeItemStateKind.Identified, TradeTriState.Yes, "identified", "true")]
    [InlineData(TradeItemStateKind.Identified, TradeTriState.No, "identified", "false")]
    public void Build_ItemStateYesAndNoUseReviewedOfficialOptionShape(
        TradeItemStateKind kind,
        TradeTriState state,
        string providerFilterId,
        string expectedOption)
    {
        var draft = Draft() with
        {
            ItemStateCriteria = new TradeItemStateCriteria().With(kind, state),
        };

        var result = BuildSuccessful(draft, providerFilterCatalog: StateFilterCatalog());

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var stateFilter = document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("misc_filters")
            .GetProperty("filters")
            .GetProperty(providerFilterId);
        Assert.Equal(expectedOption, stateFilter.GetProperty("option").GetString());
        Assert.Single(stateFilter.EnumerateObject());
    }

    [Fact]
    public void Build_AllItemStatesAnyOmitStateFiltersWithoutEmptyGroup()
    {
        var result = BuildSuccessful(Draft() with
        {
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Mirrored = TradeTriState.Any,
                Corrupted = TradeTriState.Any,
                Identified = TradeTriState.Any,
            },
        });

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var filters = document.RootElement.GetProperty("query").GetProperty("filters");
        Assert.False(filters.TryGetProperty("misc_filters", out _));
    }

    [Fact]
    public void Build_ThreeItemStatesCoexistWithCategoryRarityAndExactModifier()
    {
        var draft = WithCategoryMode(
            Draft(modifiers: [Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact)]),
            "Body Armour") with
        {
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Mirrored = TradeTriState.No,
                Corrupted = TradeTriState.Yes,
                Identified = TradeTriState.Yes,
            },
        };

        var result = BuildSuccessful(
            draft,
            selectedModifierFilters: [ProviderFilter(0, "explicit.stat_test")],
            providerFilterCatalog: StateFilterCatalog(("armour.chest", "Body Armour")));

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        var misc = query.GetProperty("filters").GetProperty("misc_filters").GetProperty("filters");
        Assert.Equal("false", misc.GetProperty("mirrored").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("corrupted").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("identified").GetProperty("option").GetString());
        Assert.Equal("rare", query.GetProperty("filters").GetProperty("type_filters")
            .GetProperty("filters").GetProperty("rarity").GetProperty("option").GetString());
        Assert.Equal("armour.chest", query.GetProperty("filters").GetProperty("type_filters")
            .GetProperty("filters").GetProperty("category").GetProperty("option").GetString());
        Assert.Equal("explicit.stat_test", query.GetProperty("stats")[0]
            .GetProperty("filters")[0].GetProperty("id").GetString());
    }

    [Theory]
    [InlineData(ParsedModifierKind.Prefix, false)]
    [InlineData(ParsedModifierKind.Implicit, true)]
    public void Build_SelectedInfluencedModifierUsesNormalStatPipelineWithoutInfluenceFilter(
        ParsedModifierKind kind,
        bool eldritch)
    {
        var modifier = Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact) with
        {
            ParsedKind = kind,
            GenerationType = kind == ParsedModifierKind.Implicit
                ? ModifierGenerationType.Implicit
                : ModifierGenerationType.Prefix,
        };
        var draft = Draft(modifiers: [modifier]) with
        {
            TraditionalInfluences = eldritch ? [] : ["Shaper Item"],
            EldritchInfluences = eldritch
                ? ["Searing Exarch Item", "Eater of Worlds Item"]
                : [],
        };

        var result = BuildSuccessful(
            draft,
            selectedModifierFilters: [ProviderFilter(0, eldritch ? "implicit.stat_test" : "explicit.stat_test")]);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal(
            eldritch ? "implicit.stat_test" : "explicit.stat_test",
            query.GetProperty("stats")[0].GetProperty("filters")[0].GetProperty("id").GetString());
        Assert.DoesNotContain("influence", result.SerializedJson!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shaper", result.SerializedJson!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("elder", result.SerializedJson!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exarch", result.SerializedJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ExactResolvedBaseName_IsPreferred()
    {
        var result = BuildSuccessful(Draft(
            parsedBaseType: "Parsed Plate",
            status: ItemBaseResolutionStatus.Exact,
            resolvedBaseName: "Catalog Plate"));

        Assert.Equal("Catalog Plate", result.Request?.Query.Type);
        Assert.Equal(ItemBaseResolutionStatus.Exact, result.SelectedBaseResolutionStatus);
    }

    [Fact]
    public void Build_CategoryMode_OmitsExactTypeAndSerializesOfficialCategoryFilter()
    {
        var result = BuildSuccessful(
            WithCategoryMode(Draft(
                parsedBaseType: "Ranger Bow",
                resolvedBaseName: "Ranger Bow",
                itemClass: "Bows"), "Bow"),
            providerFilterCatalog: CategoryCatalog(("weapon.bow", "Bow")));

        Assert.Null(result.Request?.Query.Type);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("type", out _));
        Assert.Equal("weapon.bow", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString());
        AssertRarityFilter(result.SerializedJson!, "rare");
    }

    [Fact]
    public void Build_TradeCategoryOneHandAxes_ResolvesProviderCategoryWithoutExactBase()
    {
        var result = BuildSuccessful(
            WithCategoryMode(Draft(
                rarity: "Magic",
                displayName: "Reaver Axe of Celebration",
                parsedBaseType: null,
                status: ItemBaseResolutionStatus.Probable,
                resolvedBaseName: "Reaver Axe",
                itemClass: "One Hand Axes",
                listingMode: TradeListingMode.InstantBuyout), "One Hand Axes"),
            providerFilterCatalog: CategoryCatalog(
                ("weapon.bow", "Bow"),
                ("weapon.oneaxe", "One-Handed Axe"),
                ("armour.shield", "Shield")));

        Assert.NotNull(result.Request);
        Assert.Null(result.Request.Query.Type);
        Assert.Null(result.Request.Query.Name);
        Assert.Empty(result.Request.Query.Stats[0].Filters);
        Assert.Equal("securable", result.Request.Query.Status.Option);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("type", out _));
        Assert.False(query.TryGetProperty("name", out _));
        Assert.Equal("magic", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString());
        Assert.Equal("weapon.oneaxe", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString());
        Assert.Empty(query
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.DoesNotContain("Reaver Axe", result.SerializedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Reaver Axe of Celebration", result.SerializedJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("one hand axes")]
    [InlineData("ONE HAND AXE")]
    public void Build_TradeCategoryOneHandAxes_DoesNotDependOnDisplayCasingOrCatalogOrdering(string category)
    {
        var result = BuildSuccessful(
            WithCategoryMode(Draft(
                rarity: "Magic",
                displayName: "Decorated Axe",
                parsedBaseType: null,
                status: ItemBaseResolutionStatus.Probable,
                resolvedBaseName: "Test Axe",
                itemClass: "One Hand Axes"), category),
            providerFilterCatalog: CategoryCatalog(
                ("armour.shield", "Shield"),
                ("weapon.oneaxe", "One-Handed Axe"),
                ("weapon.bow", "Bow")));

        using var document = JsonDocument.Parse(result.SerializedJson!);
        Assert.Equal("weapon.oneaxe", document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString());
    }

    [Fact]
    public void Build_ExactBaseMode_SerializesExactTypeWithoutProviderCategoryCatalog()
    {
        var result = BuildSuccessful(Draft(
            parsedBaseType: "Ranger Bow",
            resolvedBaseName: "Ranger Bow",
            itemClass: "Bows"));

        Assert.Equal("Ranger Bow", result.Request?.Query.Type);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        Assert.False(document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .TryGetProperty("category", out _));
    }

    [Fact]
    public void Build_CategoryModeUnknownCategoryFailsWithoutFallingBackToExactBase()
    {
        var result = builder.Build(
            WithCategoryMode(Draft(
                parsedBaseType: "Ranger Bow",
                resolvedBaseName: "Ranger Bow",
                itemClass: "Bows"), "Unknown Category"),
            ValidValidation(),
            League,
            providerFilterCatalog: CategoryCatalog(("weapon.bow", "Bow")));

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.UnsupportedProviderCategory);
    }

    [Fact]
    public void Build_CategoryModeUsesExactProviderLabelIndependentOfOptionOrder()
    {
        var result = BuildSuccessful(
            WithCategoryMode(Draft(
                parsedBaseType: "Prototype Base",
                resolvedBaseName: "Prototype Base",
                itemClass: "Prototype Category"), "Prototype Category"),
            providerFilterCatalog: CategoryCatalog(
                ("weapon.bow", "Bow"),
                ("prototype.category", "Prototype Category"),
                ("accessory.ring", "Ring")));

        using var document = JsonDocument.Parse(result.SerializedJson!);
        Assert.Equal("prototype.category", document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString());
    }

    [Fact]
    public void Build_ProbableResolvedBaseName_MayBeUsedWithoutChangingConfidence()
    {
        var result = BuildSuccessful(Draft(
            parsedBaseType: "Decorated Plate",
            status: ItemBaseResolutionStatus.Probable,
            resolvedBaseName: "Titan Plate"));

        Assert.Equal("Titan Plate", result.Request?.Query.Type);
        Assert.Equal(ItemBaseResolutionStatus.Probable, result.SelectedBaseResolutionStatus);
    }

    [Fact]
    public void Build_UnknownBase_FallsBackToParsedBaseText()
    {
        var result = BuildSuccessful(Draft(
            parsedBaseType: "Onyx Amulet",
            status: ItemBaseResolutionStatus.Unknown,
            resolvedBaseName: "Catalog Amulet",
            itemClass: "Amulets"));

        Assert.Equal("Onyx Amulet", result.Request?.Query.Type);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.SelectedBaseResolutionStatus);
    }

    [Fact]
    public void Build_MissingAllBaseIdentities_Fails()
    {
        var result = builder.Build(
            Draft(parsedBaseType: null, status: null, resolvedBaseName: null),
            ValidValidation(),
            League);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.MissingBaseIdentity);
    }

    [Fact]
    public void Build_InstantBuyout_MapsToSecurable()
    {
        var result = BuildSuccessful(Draft(listingMode: TradeListingMode.InstantBuyout));

        Assert.Equal("securable", result.Request?.Query.Status.Option);
    }

    [Fact]
    public void Build_InPerson_MapsToOnlineLeague()
    {
        var result = BuildSuccessful(Draft(listingMode: TradeListingMode.InPerson));

        Assert.Equal("onlineleague", result.Request?.Query.Status.Option);
    }

    [Fact]
    public void Build_NullDraft_FailsWithoutThrowing()
    {
        var exception = Record.Exception(() => builder.Build(null, ValidValidation(), League));

        Assert.Null(exception);
        AssertFailure(
            builder.Build(null, ValidValidation(), League),
            PathOfExileTradeQueryDiagnosticCodes.NullDraft);
    }

    [Fact]
    public void Build_NullValidationResult_FailsWithoutThrowing()
    {
        var exception = Record.Exception(() => builder.Build(Draft(), null, League));

        Assert.Null(exception);
        AssertFailure(
            builder.Build(Draft(), null, League),
            PathOfExileTradeQueryDiagnosticCodes.NullValidationResult);
    }

    [Fact]
    public void Build_ValidationErrors_BlockQueryCreation()
    {
        var result = builder.Build(
            Draft(),
            InvalidValidation(),
            League);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.LocallyInvalidDraft);
    }

    [Fact]
    public void Build_ValidationWarnings_DoNotBlockMappedSelectedModifiers()
    {
        var result = builder.Build(
            Draft(modifiers: [Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Unknown)]),
            TradeSearchValidationResult.FromDiagnostics(
            [
                new TradeSearchValidationDiagnostic(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved,
                    TradeSearchValidationSeverity.Warning,
                    "Local modifier did not resolve.",
                    ModifierFilterIndex: 0),
            ]),
            League,
            [ProviderFilter(0, "explicit.stat_life")]);

        Assert.True(result.IsSuccess);
        Assert.Equal("explicit.stat_life", result.Request?.Query.Stats[0].Filters[0].Id);
    }

    [Fact]
    public void Build_EmptyLeague_Fails()
    {
        var result = builder.Build(Draft(), ValidValidation(), " ");

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.MissingLeague);
    }

    [Fact]
    public void Build_SelectedModifierWithoutProviderMapping_FailsBeforeSerialization()
    {
        var result = builder.Build(
            Draft(modifiers: [Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact)]),
            ValidValidation(),
            League);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.SelectedModifiersMissingProviderMapping);
    }

    [Fact]
    public void Build_BaseGuaranteedSelectedModifierDoesNotRequireProviderMapping()
    {
        var result = BuildSuccessful(
            Draft(modifiers:
            [
                Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact) with
                {
                    ParsedKind = ParsedModifierKind.Implicit,
                    IsBaseImplicit = true,
                    ProviderResolutionStatus = SearchComponentProviderResolutionStatus.BaseGuaranteed,
                },
            ]));

        using var document = JsonDocument.Parse(result.SerializedJson!);
        Assert.Empty(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
    }

    [Fact]
    public void Build_SelectedModifierMappingCountMismatch_FailsBeforeSerialization()
    {
        var result = builder.Build(
            Draft(modifiers: [Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact)]),
            ValidValidation(),
            League,
            [
                ProviderFilter(0, "explicit.stat_one"),
                ProviderFilter(1, "explicit.stat_two"),
            ]);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.SelectedModifierMappingMismatch);
    }

    [Fact]
    public void Build_InvalidSelectedModifierMapping_FailsBeforeSerialization()
    {
        var result = builder.Build(
            Draft(modifiers: [Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact)]),
            ValidValidation(),
            League,
            [ProviderFilter(0, " ")]);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedModifierMapping);
    }

    [Fact]
    public void Build_SelectedProviderFilters_AreSerializedAsPresenceOnlyIdsInSelectedOrder()
    {
        var result = BuildSuccessful(
            Draft(modifiers:
            [
                Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact),
                Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact),
            ]),
            [
                ProviderFilter(0, "explicit.stat_life", [55m]),
                ProviderFilter(1, "explicit.stat_resistance", [38m]),
            ]);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var filters = document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(["explicit.stat_life", "explicit.stat_resistance"], filters.Select(filter =>
            filter.GetProperty("id").GetString()));
        Assert.All(filters, filter =>
        {
            Assert.False(filter.TryGetProperty("value", out _));
            Assert.False(filter.TryGetProperty("min", out _));
            Assert.False(filter.TryGetProperty("max", out _));
            Assert.False(filter.TryGetProperty("disabled", out _));
            Assert.False(filter.TryGetProperty("pseudo", out _));
            Assert.False(filter.TryGetProperty("count", out _));
            Assert.False(filter.TryGetProperty("weighted", out _));
            Assert.Single(filter.EnumerateObject());
        });
    }

    [Fact]
    public void Build_SharedPresenceFilterCoversTwoSelectedComponentsAndSerializesOnce()
    {
        var sharedFilter = ProviderFilter(0, "explicit.physical") with
        {
            SourceIndexes = [0, 1],
        };
        var result = BuildSuccessful(
            Draft(modifiers:
            [
                Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact),
                Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact),
            ]),
            [sharedFilter]);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var statFilter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());

        Assert.Equal("explicit.physical", statFilter.GetProperty("id").GetString());
        Assert.Single(statFilter.EnumerateObject());
    }

    [Fact]
    public void Build_ScalarBoundsSerializeAsInvariantJsonNumbersAndOmitEmptyValues()
    {
        var bounded = ProviderFilter(0, "explicit.stat_life") with { Minimum = -2.83m, Maximum = 60m };
        var result = BuildSuccessful(
            Draft(modifiers: [Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact)]),
            [bounded]);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var value = document.RootElement.GetProperty("query").GetProperty("stats")[0]
            .GetProperty("filters")[0].GetProperty("value");
        Assert.Equal(-2.83m, value.GetProperty("min").GetDecimal());
        Assert.Equal(60m, value.GetProperty("max").GetDecimal());
    }

    [Fact]
    public void Build_SharedPresenceFilterMissingSelectedSourceStillBlocks()
    {
        var result = builder.Build(
            Draft(modifiers:
            [
                Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact),
                Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact),
            ]),
            ValidValidation(),
            League,
            [ProviderFilter(0, "explicit.physical")]);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.SelectedModifierMappingMismatch);
    }

    [Fact]
    public void Build_RangerBowFireLocalSelectedModifierRequestMatchesPresenceOnlyParityShape()
    {
        var result = BuildSuccessful(
            Draft(
                rarity: "Rare",
                displayName: "Dread Branch",
                parsedBaseType: "Ranger Bow",
                resolvedBaseName: "Ranger Bow",
                itemClass: "Bows",
                modifiers:
                [
                    Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact),
                ]),
            [ProviderFilter(0, "explicit.stat_709508406", [70m, 139m])]);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("name", out _));
        Assert.Equal("Ranger Bow", query.GetProperty("type").GetString());
        Assert.Equal("securable", query.GetProperty("status").GetProperty("option").GetString());
        Assert.False(query.TryGetProperty("rarity", out _));
        AssertRarityFilter(result.SerializedJson!, "rare");

        var statsGroup = Assert.Single(query.GetProperty("stats").EnumerateArray());
        Assert.Equal("and", statsGroup.GetProperty("type").GetString());
        var statFilter = Assert.Single(statsGroup.GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_709508406", statFilter.GetProperty("id").GetString());
        Assert.False(statFilter.TryGetProperty("value", out _));
        Assert.False(statFilter.TryGetProperty("min", out _));
        Assert.False(statFilter.TryGetProperty("max", out _));
        Assert.Single(statFilter.EnumerateObject());
        Assert.Equal("asc", document.RootElement
            .GetProperty("sort")
            .GetProperty("price")
            .GetString());
    }

    [Fact]
    public void Build_UnselectedModifiers_DoNotAffectQuery()
    {
        var result = BuildSuccessful(Draft(modifiers:
        [
            Modifier(isSelected: false, status: ModifierCandidateResolutionStatus.Unknown),
        ]));

        Assert.Equal("Titan Plate", result.Request?.Query.Type);
        Assert.Null(result.Request?.Query.Name);
    }

    [Fact]
    public void Build_UnsupportedItemPath_FailsInsteadOfGuessing()
    {
        var result = builder.Build(
            Draft(
                rarity: "Currency",
                displayName: "Chaos Orb",
                parsedBaseType: "Chaos Orb",
                resolvedBaseName: "Chaos Orb",
                itemClass: "Stackable Currency"),
            ValidValidation(),
            League);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.UnsupportedRarityOrItemPath);
    }

    [Fact]
    public void Build_UniqueItemWithoutResolvedProviderIdentity_Fails()
    {
        var result = builder.Build(
            Draft(rarity: "Unique", displayName: " ", parsedBaseType: "Heavy Belt", resolvedBaseName: "Heavy Belt"),
            ValidValidation(),
            League);

        AssertFailure(result, PathOfExileTradeQueryDiagnosticCodes.MissingProviderUniqueIdentity);
    }

    [Fact]
    public void Build_JsonUsesCamelCase()
    {
        var result = BuildSuccessful(Draft());

        Assert.Contains("\"query\"", result.SerializedJson);
        Assert.Contains("\"status\"", result.SerializedJson);
        Assert.Contains("\"option\"", result.SerializedJson);
        Assert.DoesNotContain("\"Query\"", result.SerializedJson);
        Assert.DoesNotContain("\"Status\"", result.SerializedJson);
    }

    [Fact]
    public void Build_JsonOmitsNullName()
    {
        var result = BuildSuccessful(Draft(rarity: "Rare"));

        using var document = JsonDocument.Parse(result.SerializedJson!);
        Assert.False(document.RootElement.GetProperty("query").TryGetProperty("name", out _));
    }

    [Fact]
    public void Build_JsonContainsOneEmptyAndStatsGroup()
    {
        var result = BuildSuccessful(Draft());

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var stats = document.RootElement.GetProperty("query").GetProperty("stats");
        var statsGroup = Assert.Single(stats.EnumerateArray());
        Assert.Equal("and", statsGroup.GetProperty("type").GetString());
        Assert.Empty(statsGroup.GetProperty("filters").EnumerateArray());
    }

    [Fact]
    public void Build_JsonContainsRarityFilterForRareByDefault()
    {
        var result = BuildSuccessful(Draft());

        using var document = JsonDocument.Parse(result.SerializedJson!);
        Assert.Equal("rare", document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString());
    }

    [Theory]
    [InlineData("Normal", "normal")]
    [InlineData("Magic", "magic")]
    [InlineData("Rare", "rare")]
    public void Build_BaseOnlyNonUniqueSearchPreservesExactRarity(
        string rarity,
        string expectedProviderOption)
    {
        var result = BuildSuccessful(Draft(rarity: rarity));

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("name", out _));
        Assert.Equal("Titan Plate", query.GetProperty("type").GetString());
        AssertRarityFilter(result.SerializedJson!, expectedProviderOption);
    }

    [Fact]
    public void Build_SelectedModifierSearchPreservesRarity()
    {
        var result = BuildSuccessful(
            Draft(
                rarity: "Magic",
                displayName: "Humming Titan Plate",
                modifiers:
                [
                    Modifier(isSelected: true, status: ModifierCandidateResolutionStatus.Exact),
                ]),
            [ProviderFilter(0, "explicit.stat_life")]);

        AssertRarityFilter(result.SerializedJson!, "magic");
    }

    [Fact]
    public void Build_SortIsPriceAscending()
    {
        var result = BuildSuccessful(Draft());

        Assert.Equal("asc", result.Request?.Sort.Price);
        using var document = JsonDocument.Parse(result.SerializedJson!);
        Assert.Equal("asc", document.RootElement
            .GetProperty("sort")
            .GetProperty("price")
            .GetString());
    }

    [Fact]
    public void Build_RepeatedSerialization_IsEquivalent()
    {
        var draft = Draft();

        var first = BuildSuccessful(draft);
        var second = BuildSuccessful(draft);

        Assert.Equal(first.SerializedJson, second.SerializedJson);
    }

    [Fact]
    public void Build_ItemPropertyContributionGroupsRemainUnserializedPresentationMetadata()
    {
        var draft = Draft(
            itemClass: "One Hand Axes",
            parsedBaseType: "Reaver Axe",
            resolvedBaseName: "Reaver Axe",
            modifiers:
            [
                Modifier(isSelected: false, status: ModifierCandidateResolutionStatus.Exact),
            ]) with
        {
            ItemPropertyContributionGroups =
            [
                new TradeSearchItemPropertyContributionGroup
                {
                    ParentKind = TradeSearchItemPropertyKind.PhysicalDps,
                    Contributions =
                    [
                        new TradeSearchItemPropertyContribution
                        {
                            ModifierFilterIndex = 0,
                            Target = ItemPropertyTarget.PhysicalDamage,
                            Operation = ItemPropertyOperation.Added,
                            ReviewedSemanticDescriptorId = "test.reviewed.semantic",
                        },
                    ],
                },
            ],
        };

        var withGroups = BuildSuccessful(draft);
        var withoutGroups = BuildSuccessful(draft with { ItemPropertyContributionGroups = [] });

        Assert.Equal(withoutGroups.SerializedJson, withGroups.SerializedJson);
        Assert.DoesNotContain("test.reviewed.semantic", withGroups.SerializedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemPropertyContribution", withGroups.SerializedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void AppTradeQueryBuilder_DoesNotOwnHttpClientOrNetworkExecution()
    {
        var queryBuilderTypes = new[]
        {
            typeof(PathOfExileTradeQueryBuilder),
            typeof(PathOfExileTradeQueryBuildResult),
            typeof(PathOfExileTradeQueryDiagnostic),
        };

        Assert.DoesNotContain(queryBuilderTypes, type => Contains(type, "HttpClient"));
        Assert.DoesNotContain(
            typeof(PathOfExileTradeQueryBuilder).GetMethods(),
            method => method.Name.Contains("SearchAsync", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("SendAsync", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CoreAssembly_KeepsProviderSpecificWpfAndNetworkDependenciesOut()
    {
        var coreAssembly = typeof(TradeSearchDraft).Assembly;
        var referencedNames = coreAssembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("PresentationCore", referencedNames);
        Assert.DoesNotContain("PresentationFramework", referencedNames);
        Assert.DoesNotContain("WindowsBase", referencedNames);
        Assert.DoesNotContain("System.Net.Http", referencedNames);
        Assert.DoesNotContain(
            coreAssembly.GetTypes(),
            type => Contains(type, "PathOfExileTrade"));
    }

    [Fact]
    public void AppTradeQueryBuilder_DoesNotIntroduceCurrencyExchangeOrPublicStashModels()
    {
        Assert.DoesNotContain(
            typeof(PathOfExileTradeQueryBuilder).Assembly.GetTypes(),
            type => Contains(type, "CurrencyExchange") || Contains(type, "PublicStash"));
    }

    private PathOfExileTradeQueryBuildResult BuildSuccessful(
        TradeSearchDraft draft,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters = null,
        PathOfExileTradeItemIdentity? providerItemIdentity = null,
        PathOfExileTradeFilterCatalog? providerFilterCatalog = null)
    {
        providerItemIdentity ??= DefaultIdentityFor(draft);
        var result = builder.Build(
            draft,
            ValidValidation(),
            League,
            selectedModifierFilters,
            providerItemIdentity,
            providerFilterCatalog);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Request);
        Assert.NotNull(result.SerializedJson);
        Assert.Equal(League, result.LeagueIdentifier);
        return result;
    }

    private static PathOfExileTradeItemIdentity? DefaultIdentityFor(TradeSearchDraft draft)
    {
        if (!string.Equals(draft.Rarity?.Trim(), "Unique", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new PathOfExileTradeItemIdentity
        {
            CanonicalName = draft.DisplayName?.Trim() ?? "Test Unique",
            CanonicalType = draft.Base.ResolvedBaseName?.Trim() ?? draft.ParsedBaseType?.Trim() ?? "Test Base",
            Foulborn = TradeTriState.No,
        };
    }

    private static TradeSearchDraft Draft(
        string rarity = "Rare",
        string displayName = "Dusk Shell",
        string? parsedBaseType = "Titan Plate",
        ItemBaseResolutionStatus? status = ItemBaseResolutionStatus.Exact,
        string? resolvedBaseName = "Titan Plate",
        TradeListingMode listingMode = TradeListingMode.InstantBuyout,
        IReadOnlyList<ResolvedSearchComponent>? modifiers = null,
        string itemClass = "Body Armours")
    {
        var exactBaseName = status == ItemBaseResolutionStatus.Unknown
            ? parsedBaseType
            : resolvedBaseName ?? parsedBaseType;
        var category = itemClass switch
        {
            "Bows" => "Bow",
            "Wands" => "Wand",
            "Body Armours" => "Body Armour",
            "Rings" => "Ring",
            "Belts" => "Belt",
            "Amulets" => "Amulet",
            _ => itemClass,
        };
        return new TradeSearchDraft
        {
            ItemClass = itemClass,
            Rarity = rarity,
            DisplayName = displayName,
            ParsedBaseType = parsedBaseType,
            Base = new TradeSearchBaseDraft
            {
                Status = status,
                ResolvedBaseId = resolvedBaseName is null ? null : "base.test",
                ResolvedBaseName = resolvedBaseName,
                Category = category,
                Observed = new ObservedBaseIdentity
                {
                    Status = status,
                    ExactBaseId = resolvedBaseName is null ? null : "base.test",
                    ExactBaseName = resolvedBaseName,
                    Category = category,
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.Category,
                        Category = category,
                    },
                    ExactBase = exactBaseName is null
                        ? null
                        : new BaseSearchCriterion
                        {
                            Mode = BaseSearchMode.ExactBase,
                            Category = category,
                            ExactBaseName = exactBaseName,
                        },
                },
                ActiveCriterion = exactBaseName is null
                    ? null
                    : new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.ExactBase,
                        Category = category,
                        ExactBaseName = exactBaseName,
                    },
            },
            ModifierFilters = modifiers ?? [],
            ListingMode = listingMode,
        };
    }

    private static ResolvedSearchComponent Modifier(
        bool isSelected,
        ModifierCandidateResolutionStatus status)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "+55 to maximum Life",
            CanonicalSignature = "+<number> to maximum Life",
            ParsedKind = ParsedModifierKind.Prefix,
            ResolutionStatus = status,
            ResolvedModifierId = status == ModifierCandidateResolutionStatus.Exact
                ? "mod.test"
                : null,
            ResolvedStatIds = status == ModifierCandidateResolutionStatus.Exact
                ? ["base_maximum_life"]
                : [],
            IsSearchable = status == ModifierCandidateResolutionStatus.Exact,
            IsSelected = isSelected,
        };
    }

    private static PathOfExileTradeSelectedModifierFilter ProviderFilter(
        int sourceIndex,
        string statId,
        IReadOnlyList<decimal>? extractedNumericValues = null)
    {
        return new PathOfExileTradeSelectedModifierFilter
        {
            SourceIndex = sourceIndex,
            StatId = statId,
            OriginalText = "+55 to maximum Life",
            NormalizedItemTemplate = "+# to maximum Life",
            ExtractedNumericValues = extractedNumericValues ?? [],
        };
    }

    private static TradeSearchDraft WithCategoryMode(
        TradeSearchDraft draft,
        string category)
    {
        var exactBaseName = draft.Base.ResolvedBaseName ?? draft.ParsedBaseType;
        return draft with
        {
            Base = draft.Base with
            {
                Category = category,
                ActiveCriterion = new BaseSearchCriterion
                {
                    Mode = BaseSearchMode.Category,
                    Category = category,
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.Category,
                        Category = category,
                    },
                    ExactBase = exactBaseName is null
                        ? null
                        : new BaseSearchCriterion
                        {
                            Mode = BaseSearchMode.ExactBase,
                            Category = category,
                            ExactBaseName = exactBaseName,
                        },
                },
            },
        };
    }

    private static PathOfExileTradeFilterCatalog CategoryCatalog(
        params (string Id, string Text)[] categories)
    {
        return new PathOfExileTradeFilterCatalog(categories
            .Select((category, index) => new PathOfExileTradeFilterOption
            {
                ProviderOrder = index,
                GroupId = "type_filters",
                FilterId = "category",
                Id = category.Id,
                Text = category.Text,
            }));
    }

    private static PathOfExileTradeFilterCatalog StateFilterCatalog(
        params (string Id, string Text)[] categories)
    {
        return new PathOfExileTradeFilterCatalog(
            categories.Select((category, index) => new PathOfExileTradeFilterOption
            {
                ProviderOrder = index,
                GroupId = "type_filters",
                FilterId = "category",
                Id = category.Id,
                Text = category.Text,
            }),
            optionFilterDefinitions: new[]
            {
                OptionDefinition(0, "identified", "Identified"),
                OptionDefinition(1, "corrupted", "Corrupted"),
                OptionDefinition(2, "mirrored", "Mirrored"),
            });
    }

    private static PathOfExileTradeOptionFilterDefinition OptionDefinition(
        int providerOrder,
        string id,
        string text)
    {
        return new PathOfExileTradeOptionFilterDefinition
        {
            GroupProviderOrder = 1,
            ProviderOrder = providerOrder,
            GroupId = "misc_filters",
            GroupTitle = "Miscellaneous",
            FilterId = id,
            Text = text,
            Options =
            [
                new PathOfExileTradeOptionDefinition { Id = null, Text = "Any" },
                new PathOfExileTradeOptionDefinition { Id = "true", Text = "Yes" },
                new PathOfExileTradeOptionDefinition { Id = "false", Text = "No" },
            ],
        };
    }

    private static TradeSearchValidationResult ValidValidation()
    {
        return TradeSearchValidationResult.FromDiagnostics([]);
    }

    private static TradeSearchValidationResult InvalidValidation()
    {
        return TradeSearchValidationResult.FromDiagnostics(
        [
            new TradeSearchValidationDiagnostic(
                TradeSearchValidationDiagnosticCodes.MissingBaseIdentity,
                TradeSearchValidationSeverity.Error,
                "Invalid for test."),
        ]);
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

    private static void AssertRarityFilter(
        string serializedJson,
        string expectedProviderOption)
    {
        using var document = JsonDocument.Parse(serializedJson);
        Assert.Equal(expectedProviderOption, document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString());
    }

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }
}

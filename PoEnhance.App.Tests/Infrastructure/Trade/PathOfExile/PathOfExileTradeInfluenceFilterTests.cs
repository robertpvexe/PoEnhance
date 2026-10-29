using System.Collections.Immutable;
using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeInfluenceFilterTests
{
    [Fact]
    public void Resolver_MapsTraditionalInfluenceToReviewedPseudoStat()
    {
        var resolver = new PathOfExileTradeInfluenceFilterResolver();
        var draft = DraftWithInfluences(
            TradeSearchInfluenceCriteriaFactory.Create(["Shaper Item"], []));

        var result = resolver.MapSelected(draft, TraditionalStatCatalog(), filterCatalog: null);

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal(TradeSearchInfluenceKind.Shaper, filter.SourceKind);
        Assert.Equal(TradeSearchInfluenceFamily.Traditional, filter.Family);
        Assert.Equal("pseudo.pseudo_has_shaper_influence", filter.StatId);
        Assert.Equal(
            TradeSearchItemPropertyProviderResolutionStatus.Exact,
            Assert.Single(result.Draft!.InfluenceCriteria).ProviderResolutionStatus);
    }

    [Fact]
    public void Resolver_MapsEldritchInfluencesToReviewedMiscFilters()
    {
        var resolver = new PathOfExileTradeInfluenceFilterResolver();
        var draft = DraftWithInfluences(
            TradeSearchInfluenceCriteriaFactory.Create(
                [],
                ["Searing Exarch Item", "Eater of Worlds Item"]));

        var result = resolver.MapSelected(draft, statCatalog: null, EldritchFilterCatalog());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Filters.Count);
        Assert.Contains(result.Filters, filter =>
            filter.SourceKind == TradeSearchInfluenceKind.SearingExarch &&
            filter.ProviderGroupId == "misc_filters" &&
            filter.ProviderFilterId == "searing_item" &&
            filter.Option == "true");
        Assert.Contains(result.Filters, filter =>
            filter.SourceKind == TradeSearchInfluenceKind.EaterOfWorlds &&
            filter.ProviderGroupId == "misc_filters" &&
            filter.ProviderFilterId == "tangled_item" &&
            filter.Option == "true");
    }

    [Fact]
    public void Resolver_DisabledInfluenceIsOmittedWithoutFailing()
    {
        var resolver = new PathOfExileTradeInfluenceFilterResolver();
        var criteria = TradeSearchInfluenceCriteriaFactory.Create(["Elder Item"], []);
        var draft = DraftWithInfluences(criteria
            .Select(criterion => criterion with { IsEnabled = false })
            .ToImmutableArray());

        var result = resolver.MapSelected(draft, TraditionalStatCatalog(), filterCatalog: null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Filters);
    }

    [Fact]
    public void Resolver_MissingCatalogMarksEnabledTraditionalUnsupported()
    {
        var resolver = new PathOfExileTradeInfluenceFilterResolver();
        var draft = DraftWithInfluences(
            TradeSearchInfluenceCriteriaFactory.Create(["Hunter Item"], []));

        var result = resolver.MapSelected(draft, statCatalog: null, filterCatalog: null);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            Assert.Single(result.Draft!.InfluenceCriteria).ProviderResolutionStatus);
    }

    [Fact]
    public void Resolver_IncompatibleStatCatalogMarksUnsupportedWithoutSilentSuccess()
    {
        var resolver = new PathOfExileTradeInfluenceFilterResolver();
        var draft = DraftWithInfluences(
            TradeSearchInfluenceCriteriaFactory.Create(["Warlord Item"], []));
        var incompatibleCatalog = new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                Id = "pseudo.pseudo_has_warlord_influence",
                Text = "Wrong Warlord Text",
                Type = "pseudo",
            },
        ]);

        var result = resolver.MapSelected(draft, incompatibleCatalog, filterCatalog: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
            Assert.Single(result.Draft!.InfluenceCriteria).ProviderResolutionStatus);
    }

    [Fact]
    public void Build_SingleTraditionalInfluenceEmitsExactPseudoStatInAndGroup()
    {
        var draft = DraftWithInfluences(
            TradeSearchInfluenceCriteriaFactory.Create(["Redeemer Item"], []));
        var mapped = new PathOfExileTradeInfluenceFilterResolver()
            .MapSelected(draft, TraditionalStatCatalog(), filterCatalog: null);
        Assert.True(mapped.IsSuccess);

        var result = BuildSuccessful(
            mapped.Draft!,
            selectedInfluenceFilters: mapped.Filters);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var filters = document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters");
        var filter = Assert.Single(filters.EnumerateArray());
        Assert.Equal("pseudo.pseudo_has_redeemer_influence", filter.GetProperty("id").GetString());
        Assert.False(filter.TryGetProperty("value", out _));
        Assert.DoesNotContain("misc_filters", result.SerializedJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DoubleTraditionalInfluencesEmitTwoSimultaneousAndConditions()
    {
        var draft = DraftWithInfluences(
            TradeSearchInfluenceCriteriaFactory.Create(["Shaper Item", "Elder Item"], []));
        var mapped = new PathOfExileTradeInfluenceFilterResolver()
            .MapSelected(draft, TraditionalStatCatalog(), filterCatalog: null);
        Assert.True(mapped.IsSuccess);
        Assert.Equal(2, mapped.Filters.Count);

        var result = BuildSuccessful(
            mapped.Draft!,
            selectedInfluenceFilters: mapped.Filters);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var filters = document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters")
            .EnumerateArray()
            .Select(filter => filter.GetProperty("id").GetString())
            .ToArray();
        Assert.Equal(
            [
                "pseudo.pseudo_has_shaper_influence",
                "pseudo.pseudo_has_elder_influence",
            ],
            filters);
        Assert.DoesNotContain("\"type\":\"count\"", result.SerializedJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("pseudo_has_influence_count", result.SerializedJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_TogglingOneDoubleInfluenceRemovesOnlyThatCondition()
    {
        var criteria = TradeSearchInfluenceCriteriaFactory.Create(["Shaper Item", "Elder Item"], []);
        var draft = DraftWithInfluences(criteria
            .Select(criterion =>
                criterion.Kind == TradeSearchInfluenceKind.Elder
                    ? criterion with { IsEnabled = false }
                    : criterion)
            .ToImmutableArray());
        var mapped = new PathOfExileTradeInfluenceFilterResolver()
            .MapSelected(draft, TraditionalStatCatalog(), filterCatalog: null);
        Assert.True(mapped.IsSuccess);

        var result = BuildSuccessful(
            mapped.Draft!,
            selectedInfluenceFilters: mapped.Filters);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var filter = Assert.Single(
            document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("pseudo.pseudo_has_shaper_influence", filter.GetProperty("id").GetString());
        Assert.DoesNotContain("pseudo_has_elder_influence", result.SerializedJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AllInfluencesOffOmitInfluenceCriteria()
    {
        var criteria = TradeSearchInfluenceCriteriaFactory.Create(["Crusader Item"], []);
        var draft = DraftWithInfluences(criteria
            .Select(criterion => criterion with { IsEnabled = false })
            .ToImmutableArray());

        var result = BuildSuccessful(draft, selectedInfluenceFilters: []);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        Assert.Empty(
            document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.DoesNotContain("pseudo_has_", result.SerializedJson!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("searing_item", result.SerializedJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("tangled_item", result.SerializedJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EldritchInfluencesUseMiscFiltersWithoutDuplicatingLowerUiSemantics()
    {
        var draft = DraftWithInfluences(
            TradeSearchInfluenceCriteriaFactory.Create(
                [],
                ["Searing Exarch Item", "Eater of Worlds Item"]));
        var mapped = new PathOfExileTradeInfluenceFilterResolver()
            .MapSelected(draft, statCatalog: null, EldritchFilterCatalog());
        Assert.True(mapped.IsSuccess);

        var result = BuildSuccessful(
            mapped.Draft!,
            providerFilterCatalog: EldritchFilterCatalog(),
            selectedInfluenceFilters: mapped.Filters);

        using var document = JsonDocument.Parse(result.SerializedJson!);
        var misc = document.RootElement
            .GetProperty("query")
            .GetProperty("filters")
            .GetProperty("misc_filters")
            .GetProperty("filters");
        Assert.Equal("true", misc.GetProperty("searing_item").GetProperty("option").GetString());
        Assert.Equal("true", misc.GetProperty("tangled_item").GetProperty("option").GetString());
        Assert.Empty(
            document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.DoesNotContain("pseudo_has_", result.SerializedJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_EnabledInfluenceWithoutProviderMappingFailsInsteadOfOmitting()
    {
        var draft = DraftWithInfluences(
            TradeSearchInfluenceCriteriaFactory.Create(["Shaper Item"], []));

        var result = new PathOfExileTradeQueryBuilder().Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            "Standard",
            selectedModifierFilters: null,
            providerItemIdentity: null,
            providerFilterCatalog: null,
            selectedItemPropertyFilters: null,
            selectedRequestedItemFilters: null,
            selectedInfluenceFilters: []);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code ==
                PathOfExileTradeQueryDiagnosticCodes.SelectedInfluencesMissingProviderMapping);
    }

    private static PathOfExileTradeQueryBuildResult BuildSuccessful(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog? providerFilterCatalog = null,
        IReadOnlyList<PathOfExileTradeSelectedInfluenceFilter>? selectedInfluenceFilters = null)
    {
        var result = new PathOfExileTradeQueryBuilder().Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            "Standard",
            selectedModifierFilters: null,
            providerItemIdentity: null,
            providerFilterCatalog: providerFilterCatalog,
            selectedItemPropertyFilters: null,
            selectedRequestedItemFilters: null,
            selectedInfluenceFilters: selectedInfluenceFilters);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.False(string.IsNullOrWhiteSpace(result.SerializedJson));
        return result;
    }

    private static TradeSearchDraft DraftWithInfluences(
        ImmutableArray<TradeSearchInfluenceCriterion> criteria)
    {
        return new TradeSearchDraft
        {
            ItemClass = "Body Armours",
            Rarity = "Rare",
            DisplayName = "Influence Shell",
            ParsedBaseType = "Titan Plate",
            TraditionalInfluences = criteria
                .Where(criterion => criterion.Family == TradeSearchInfluenceFamily.Traditional)
                .Select(criterion => criterion.ObservedLine)
                .ToArray(),
            EldritchInfluences = criteria
                .Where(criterion => criterion.Family == TradeSearchInfluenceFamily.Eldritch)
                .Select(criterion => criterion.ObservedLine)
                .ToArray(),
            InfluenceCriteria = criteria,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.test",
                ResolvedBaseName = "Titan Plate",
                Category = "Body Armour",
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "base.test",
                    ExactBaseName = "Titan Plate",
                    Category = "Body Armour",
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.Category,
                        Category = "Body Armour",
                    },
                    ExactBase = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.ExactBase,
                        Category = "Body Armour",
                        ExactBaseName = "Titan Plate",
                    },
                },
                ActiveCriterion = new BaseSearchCriterion
                {
                    Mode = BaseSearchMode.ExactBase,
                    Category = "Body Armour",
                    ExactBaseName = "Titan Plate",
                },
            },
        };
    }

    private static PathOfExileTradeStatCatalog TraditionalStatCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            Stat("pseudo.pseudo_has_shaper_influence", "Has Shaper Influence"),
            Stat("pseudo.pseudo_has_elder_influence", "Has Elder Influence"),
            Stat("pseudo.pseudo_has_crusader_influence", "Has Crusader Influence"),
            Stat("pseudo.pseudo_has_redeemer_influence", "Has Redeemer Influence"),
            Stat("pseudo.pseudo_has_hunter_influence", "Has Hunter Influence"),
            Stat("pseudo.pseudo_has_warlord_influence", "Has Warlord Influence"),
        ]);
    }

    private static PathOfExileTradeStatEntry Stat(string id, string text) =>
        new()
        {
            ProviderOrder = 0,
            Id = id,
            Text = text,
            Type = "pseudo",
        };

    private static PathOfExileTradeFilterCatalog EldritchFilterCatalog()
    {
        return new PathOfExileTradeFilterCatalog(
            [],
            optionFilterDefinitions:
            [
                Option("searing_item", "Searing Exarch Item"),
                Option("tangled_item", "Eater of Worlds Item"),
            ]);
    }

    private static PathOfExileTradeOptionFilterDefinition Option(string id, string text) =>
        new()
        {
            GroupProviderOrder = 1,
            ProviderOrder = 0,
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

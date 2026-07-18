using System.Text.Json;
using System.Text.RegularExpressions;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeQueryBuilderCategoryProductionTests
{
    [Fact]
    public async Task PriceCheckerProductionPath_TradeCategoryMagicOneHandAxeZeroModifiersCreatesCategoryOnlyRequest()
    {
        var fixture = ProductionTradeCategoryFixture.Create(new PathOfExileTradeStatCatalog([]));
        fixture.Controller.UpdateCurrentDraft(fixture.MagicReaverAxeDraft, fixture.MagicReaverAxeValidation);
        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
        Assert.NotEqual("Select a supported Trade search.", fixture.Window.CurrentSearchState?.Message);
        var call = Assert.Single(fixture.SearchClient.Calls);
        var json = PathOfExileTradeJson.SerializeSearchRequest(call.Request!);
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");

        Assert.False(query.TryGetProperty("type", out _));
        Assert.False(query.TryGetProperty("name", out _));
        Assert.Equal("securable", query.GetProperty("status").GetProperty("option").GetString());
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
        Assert.DoesNotContain("Reaver Axe", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Reaver Axe of Celebration", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PriceCheckerProductionPath_TradeCategoryMagicOneHandAxeSelectedAttackSpeedDoesNotFailAsUnsupportedCategory()
    {
        var fixture = ProductionTradeCategoryFixture.Create(AttackSpeedStatCatalog());
        fixture.Controller.UpdateCurrentDraft(fixture.MagicReaverAxeDraft, fixture.MagicReaverAxeValidation);
        var row = ExpandAttackSpeedChild(fixture);
        Assert.True(row.SupportsValueBounds);
        Assert.Equal("26", row.MinimumText);
        Assert.Empty(row.MaximumText);
        fixture.Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: true);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.False(Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.AttacksPerSecond).IsSelected);

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
        Assert.NotEqual("Select a supported Trade search.", fixture.Window.CurrentSearchState?.Message);
        var call = Assert.Single(fixture.SearchClient.Calls);
        var json = PathOfExileTradeJson.SerializeSearchRequest(call.Request!);
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");

        Assert.False(query.TryGetProperty("type", out _));
        Assert.False(query.TryGetProperty("name", out _));
        Assert.Equal("securable", query.GetProperty("status").GetProperty("option").GetString());
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
        var statsGroup = Assert.Single(query.GetProperty("stats").EnumerateArray());
        Assert.Equal("and", statsGroup.GetProperty("type").GetString());
        var statFilter = Assert.Single(statsGroup.GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_210067635", statFilter.GetProperty("id").GetString());
        var value = statFilter.GetProperty("value");
        Assert.Equal(26m, value.GetProperty("min").GetDecimal());
        Assert.False(value.TryGetProperty("max", out _));
        Assert.False(statFilter.TryGetProperty("min", out _));
        Assert.False(statFilter.TryGetProperty("max", out _));
        Assert.Equal(2, statFilter.EnumerateObject().Count());
        Assert.Single(statsGroup.GetProperty("filters").EnumerateArray());
    }

    [Fact]
    public async Task PriceCheckerProductionPath_CraftedAttackSpeedAndManaLeechEmitObservedMinimums()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                Stat(0, "explicit.stat_mana_leech", "#% of Physical Attack Damage Leeched as Mana (Local)"),
                CraftedStat(1, "crafted.stat_210067635", "#% increased Attack Speed (Local)"),
                PseudoStat(2, "pseudo.pseudo_total_attack_speed", "+#% total Attack Speed"),
            ]),
            ArmageddonThirstCraftedAttackSpeedText,
            expectedRarity: "Rare",
            expectedModifierCount: 2);
        fixture.Controller.UpdateCurrentDraft(fixture.MagicReaverAxeDraft, fixture.MagicReaverAxeValidation);
        var leech = Assert.Single(fixture.Window.CurrentSearchState!.Modifiers, row =>
            row.Text.Contains("Leeched as Mana", StringComparison.Ordinal));
        var attackSpeed = ExpandAttackSpeedChild(fixture);
        Assert.Equal("2.83", leech.MinimumText);
        Assert.Equal("20", attackSpeed.MinimumText);
        Assert.True(leech.SupportsValueBounds);
        Assert.True(attackSpeed.SupportsValueBounds);
        fixture.Window.RaiseModifierSelectionChanged(leech.SourceIndex, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(attackSpeed.SourceIndex, isSelected: true);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.False(Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.AttacksPerSecond).IsSelected);

        await fixture.Controller.SearchAsync();

        Assert.True(
            fixture.Window.CurrentSearchState?.Status == PriceCheckerSearchViewStatus.ZeroResults,
            $"{fixture.Window.CurrentSearchState?.Status}: {fixture.Window.CurrentSearchState?.Message}");
        var call = Assert.Single(fixture.SearchClient.Calls);
        using var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(call.Request!));
        var filters = document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToDictionary(filter => filter.GetProperty("id").GetString()!);
        Assert.Equal(2.83m, filters["explicit.stat_mana_leech"]
            .GetProperty("value")
            .GetProperty("min")
            .GetDecimal());
        Assert.Equal(20m, filters["crafted.stat_210067635"]
            .GetProperty("value")
            .GetProperty("min")
            .GetDecimal());
    }

    [Fact]
    public async Task ArmageddonThirst_CraftedLocalAttackSpeedDoesNotOfferBroadPseudoAndSearchesCrafted()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                Stat(0, "explicit.stat_mana_leech", "#% of Physical Attack Damage Leeched as Mana (Local)"),
                CraftedStat(1, "crafted.stat_210067635", "#% increased Attack Speed (Local)"),
                PseudoStat(2, "pseudo.pseudo_total_attack_speed", "+#% total Attack Speed"),
            ]),
            ArmageddonThirstCraftedAttackSpeedText,
            expectedRarity: "Rare",
            expectedModifierCount: 2);
        fixture.Controller.UpdateCurrentDraft(fixture.MagicReaverAxeDraft, fixture.MagicReaverAxeValidation);
        var attackSpeed = ExpandAttackSpeedChild(fixture);
        Assert.Equal("20", attackSpeed.MinimumText);

        fixture.Window.RaiseModifierSelectionChanged(attackSpeed.SourceIndex, isSelected: true);
        Assert.Empty(fixture.SearchClient.Calls);
        await fixture.Controller.SearchAsync();

        Assert.Single(fixture.SearchClient.Calls);
        attackSpeed = fixture.Window.FindModifier(attackSpeed.SourceIndex);
        Assert.Equal("Crafted", attackSpeed.SelectedFilterVariant?.Label);
        Assert.DoesNotContain(attackSpeed.FilterVariants, option => option.Label == "Pseudo");
        Assert.Equal("20", attackSpeed.MinimumText);
        Assert.Equal(string.Empty, attackSpeed.MaximumText);
        var call = Assert.Single(fixture.SearchClient.Calls);
        var serializedJson = PathOfExileTradeJson.SerializeSearchRequest(call.Request!);
        using var document = JsonDocument.Parse(serializedJson);
        var statGroup = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray());
        Assert.Equal("and", statGroup.GetProperty("type").GetString());
        var filter = Assert.Single(statGroup.GetProperty("filters").EnumerateArray());
        Assert.Equal("crafted.stat_210067635", filter.GetProperty("id").GetString());
        Assert.Equal(20m, filter.GetProperty("value").GetProperty("min").GetDecimal());
        Assert.DoesNotContain("pseudo.pseudo_total_attack_speed", serializedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArmageddonThirst_CraftedLocalAttackSpeedUsesApplicableGameDataFamiliesAndDistinctKinds()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                ProviderStat(0, "crafted.stat_210067635", "#% increased Attack Speed (Local)", "Crafted"),
                ProviderStat(1, "crafted.stat_681332047", "#% increased Attack Speed", "Crafted"),
                ProviderStat(2, "explicit.stat_210067635", "#% increased Attack Speed (Local)", "Explicit"),
                ProviderStat(3, "explicit.stat_681332047", "#% increased Attack Speed", "Explicit"),
                ProviderStat(4, "implicit.stat_210067635", "#% increased Attack Speed (Local)", "Implicit"),
                ProviderStat(5, "implicit.stat_681332047", "#% increased Attack Speed", "Implicit"),
                ProviderStat(6, "fractured.stat_210067635", "#% increased Attack Speed (Local)", "Fractured"),
                ProviderStat(7, "fractured.stat_681332047", "#% increased Attack Speed", "Fractured"),
                ProviderStat(8, "enchant.stat_210067635", "#% increased Attack Speed (Local)", "Enchant"),
                ProviderStat(9, "scourge.stat_681332047", "#% increased Attack Speed", "Scourge"),
                ProviderStat(10, "pseudo.pseudo_total_attack_speed", "+#% total Attack Speed", "Pseudo"),
            ]),
            ArmageddonThirstCraftedAttackSpeedText,
            expectedRarity: "Rare",
            expectedModifierCount: 2);
        var source = Assert.Single(fixture.MagicReaverAxeDraft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("increased Attack Speed", StringComparison.Ordinal));

        Assert.Contains(source.ProviderDomainEvidence, evidence =>
            evidence.ProviderDomain == "Crafted" && evidence.IsSourceExact);
        Assert.Contains(source.ProviderDomainEvidence, evidence =>
            evidence.ProviderDomain == "Explicit" && !evidence.IsSourceExact);
        Assert.Contains(source.ProviderDomainEvidence, evidence =>
            evidence.ProviderDomain == "Fractured" && evidence.IsProjectedDomain);
        Assert.Contains(source.ProviderDomainEvidence, evidence =>
            evidence.ProviderDomain == "Implicit" && !evidence.IsSourceExact);
        Assert.DoesNotContain(source.ProviderDomainEvidence, evidence =>
            evidence.ProviderDomain is "Enchant" or "Scourge");

        var prepared = await fixture.Controller.PrepareDraftAsync(fixture.MagicReaverAxeDraft);
        var attackSpeed = Assert.Single(prepared.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("increased Attack Speed", StringComparison.Ordinal));

        Assert.Equal("crafted.stat_210067635", attackSpeed.ProviderStatId);
        Assert.Equal(["Crafted", "Explicit", "Fractured", "Implicit"], attackSpeed.FilterVariants
            .Select(option => option.Label)
            .OrderBy(label => label, StringComparer.Ordinal));
        Assert.DoesNotContain(attackSpeed.FilterVariants, option => option.Label == "Pseudo");
        Assert.Equal(
            attackSpeed.FilterVariants.Count,
            attackSpeed.FilterVariants.Select(option => option.Label).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ArmageddonThirst_StaleBroadPseudoRemainsUnavailableAndBlocksOnlyWhileSelected()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                Stat(0, "explicit.stat_mana_leech", "#% of Physical Attack Damage Leeched as Mana (Local)"),
                CraftedStat(1, "crafted.stat_210067635", "#% increased Attack Speed (Local)"),
                PseudoStat(2, "pseudo.pseudo_total_attack_speed", "+#% total Attack Speed"),
            ]),
            ArmageddonThirstCraftedAttackSpeedText,
            expectedRarity: "Rare",
            expectedModifierCount: 2);
        var attackSpeedIndex = fixture.MagicReaverAxeDraft.ModifierFilters
            .Select((modifier, index) => new { Modifier = modifier, Index = index })
            .Single(entry => entry.Modifier.OriginalText.Contains(
                "increased Attack Speed",
                StringComparison.Ordinal))
            .Index;
        var staleIdentity = PathOfExileTradeModifierVariantResolver.IdentityFor(
            "pseudo.pseudo_total_attack_speed");
        var staleDraft = fixture.MagicReaverAxeDraft with
        {
            ModifierFilters = fixture.MagicReaverAxeDraft.ModifierFilters
                .Select((modifier, index) => index == attackSpeedIndex
                    ? modifier with { SelectedFilterVariantIdentity = staleIdentity }
                    : modifier)
                .ToArray(),
        };

        var prepared = await fixture.Controller.PrepareDraftAsync(staleDraft);
        var stale = prepared.ModifierFilters[attackSpeedIndex];

        Assert.Equal(staleIdentity, stale.SelectedFilterVariantIdentity);
        Assert.Equal(SearchComponentProviderResolutionStatus.NotFound, stale.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
            stale.ProviderDiagnosticCode);
        Assert.Null(stale.ProviderStatId);
        Assert.DoesNotContain(stale.FilterVariants, option => option.Label == "Pseudo");

        fixture.Controller.UpdateCurrentDraft(prepared, new TradeSearchDraftValidator().Validate(prepared));
        fixture.Controller.UpdateModifierSelection(attackSpeedIndex, isSelected: true);
        Assert.False(fixture.Window.FindModifier(attackSpeedIndex).IsSelected);
        Assert.False(fixture.Window.FindModifier(attackSpeedIndex).IsInteractionEnabled);
        Assert.True(fixture.Window.CurrentSearchState!.CanSearch);
        Assert.Empty(fixture.SearchClient.Calls);

        fixture.Controller.UpdateModifierSelection(attackSpeedIndex, isSelected: false);
        var manaLeechIndex = prepared.ModifierFilters
            .Select((modifier, index) => new { Modifier = modifier, Index = index })
            .Single(entry => entry.Modifier.OriginalText.Contains("Leeched as Mana", StringComparison.Ordinal))
            .Index;
        fixture.Controller.UpdateModifierSelection(manaLeechIndex, isSelected: true);
        Assert.True(fixture.Window.CurrentSearchState.CanSearch);
        await fixture.Controller.SearchAsync();
        Assert.Single(fixture.SearchClient.Calls);
    }

    [Fact]
    public async Task HorrorMangler_PseudoParentAndSelectedFixedCraftedContributorEmitTwoAndFilters()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                ProviderStat(0, "explicit.stat_1509134228", "#% increased Physical Damage", "Explicit"),
                ProviderStat(1, "crafted.stat_1509134228", "#% increased Physical Damage", "Crafted"),
                ProviderStat(2, "fractured.stat_1509134228", "#% increased Physical Damage", "Fractured"),
                ProviderStat(3, "pseudo.local_increased_physical_damage", "#% total increased Physical Damage (Local)", "Pseudo"),
                ProviderStat(4, "explicit.stat_803737631", "+# to Accuracy Rating", "Explicit"),
            ]),
            Features.PriceChecking.PriceCheckerProductionPathCorpusTests
                .HorrorManglerExplicitAndCraftedPhysicalDamageText,
            expectedRarity: "Rare",
            expectedModifierCount: 2);
        var prepared = await fixture.Controller.PrepareDraftAsync(fixture.MagicReaverAxeDraft);
        var accuracy = Assert.Single(prepared.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Accuracy Rating", StringComparison.Ordinal));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, accuracy.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_803737631", accuracy.ProviderStatId);
        Assert.Null(accuracy.ProviderDiagnosticCode);
        var parentIndex = prepared.ModifierFilters
            .Select((modifier, index) => new { Modifier = modifier, Index = index })
            .Single(entry => entry.Modifier.OriginalText.Contains(
                "increased Physical Damage",
                StringComparison.Ordinal))
            .Index;
        var parent = prepared.ModifierFilters[parentIndex];

        Assert.Equal(
            ["Crafted", "Explicit", "Fractured", "Pseudo"],
            parent.FilterVariants.Select(option => option.Label).OrderBy(label => label, StringComparer.Ordinal));
        Assert.Equal("Pseudo", Assert.Single(parent.FilterVariants, option =>
            option.Identity == parent.SelectedFilterVariantIdentity).Label);
        Assert.Equal(
            parent.FilterVariants.Count,
            parent.FilterVariants.Select(option => option.Label).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, parent.Contributors.Count);
        Assert.Equal(
            [
                PathOfExileTradeProviderIdentity.Create("explicit.stat_1509134228"),
                PathOfExileTradeProviderIdentity.Create("crafted.stat_1509134228"),
            ],
            parent.Contributors.Select(contributor => contributor.ProviderIdentity));
        Assert.All(parent.Contributors, contributor =>
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, contributor.ProviderResolutionStatus));

        fixture.Controller.UpdateCurrentDraft(prepared, new TradeSearchDraftValidator().Validate(prepared));
        fixture.Controller.UpdateModifierSelection(parentIndex, isSelected: true);
        var selectedParentRow = fixture.Window.FindModifier(parentIndex);
        Assert.Equal(
            parent.FilterVariants.Select(option => (option.Identity, option.Label)),
            selectedParentRow.FilterVariants.Select(option => (option.Identity, option.Label)));
        Assert.True(selectedParentRow.HasMultipleFilterVariants);
        Assert.True(selectedParentRow.CanSelectFilterVariant);
        fixture.Controller.UpdateModifierContributorSelection(parentIndex, 1, isSelected: true);

        var row = fixture.Window.FindModifier(parentIndex);
        Assert.Equal(
            parent.FilterVariants.Select(option => (option.Identity, option.Label)),
            row.FilterVariants.Select(option => (option.Identity, option.Label)));
        Assert.True(row.IsSelected);
        Assert.True(row.HasMultipleFilterVariants);
        Assert.True(row.CanSelectFilterVariant);
        Assert.Equal("116", row.MinimumText);
        Assert.Equal("116", row.Contributors[1].MinimumText);
        Assert.Equal("Crafted Prefix", row.Contributors[1].ProvenanceLabel);
        Assert.True(row.Contributors[1].IsInteractionEnabled);
        Assert.True(fixture.Window.CurrentSearchState!.CanSearch);
        Assert.Empty(fixture.SearchClient.Calls);
        var currentValidation = new TradeSearchDraftValidator().Validate(fixture.Window.CurrentState!.Draft);
        Assert.True(
            currentValidation.IsValid,
            string.Join(" | ", currentValidation.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}")));

        await fixture.Controller.SearchAsync();

        Assert.True(
            fixture.Window.CurrentSearchState?.Status == PriceCheckerSearchViewStatus.ZeroResults,
            $"{fixture.Window.CurrentSearchState?.Status}: {fixture.Window.CurrentSearchState?.Message}");
        var call = Assert.Single(fixture.SearchClient.Calls);
        using var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(call.Request!));
        var statGroup = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray());
        Assert.Equal("and", statGroup.GetProperty("type").GetString());
        var filters = statGroup.GetProperty("filters").EnumerateArray().ToArray();
        Assert.Equal(2, filters.Length);
        Assert.Equal(
            ["pseudo.local_increased_physical_damage", "crafted.stat_1509134228"],
            filters.Select(filter => filter.GetProperty("id").GetString()));
        Assert.Equal(
            [116m, 116m],
            filters.Select(filter => filter.GetProperty("value").GetProperty("min").GetDecimal()));
        Assert.Equal(2, filters.Select(filter => filter.GetProperty("id").GetString()).Distinct().Count());

        var fractured = Assert.Single(parent.FilterVariants, option => option.Label == "Fractured");
        fixture.Controller.UpdateModifierFilterVariant(parentIndex, fractured.Identity);

        var standaloneRow = fixture.Window.FindModifier(parentIndex);
        Assert.Equal("Fractured", standaloneRow.SelectedFilterVariant?.Label);
        Assert.Equal("116", standaloneRow.MinimumText);
        Assert.True(standaloneRow.Contributors[1].IsSelected);
        Assert.True(standaloneRow.Contributors[1].IsInactive);
        Assert.False(standaloneRow.Contributors[1].CanEditBounds);
        Assert.Single(fixture.SearchClient.Calls);

        await fixture.Controller.SearchAsync();

        Assert.Equal(2, fixture.SearchClient.Calls.Count);
        var standaloneCall = fixture.SearchClient.Calls[1];
        using var standaloneDocument = JsonDocument.Parse(
            PathOfExileTradeJson.SerializeSearchRequest(standaloneCall.Request!));
        var standaloneFilters = standaloneDocument.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        var standaloneFilter = Assert.Single(standaloneFilters);
        Assert.Equal("fractured.stat_1509134228", standaloneFilter.GetProperty("id").GetString());
        Assert.Equal(116m, standaloneFilter.GetProperty("value").GetProperty("min").GetDecimal());

        var pseudo = Assert.Single(parent.FilterVariants, option => option.Label == "Pseudo");
        fixture.Controller.UpdateModifierFilterVariant(parentIndex, pseudo.Identity);

        var restoredRow = fixture.Window.FindModifier(parentIndex);
        Assert.Equal("116", restoredRow.MinimumText);
        Assert.True(restoredRow.Contributors[1].IsSelected);
        Assert.True(restoredRow.Contributors[1].IsInteractionEnabled);
        Assert.Equal(2, fixture.SearchClient.Calls.Count);

        fixture.Controller.UpdateModifierContributorSelection(parentIndex, 1, isSelected: false);
        fixture.Controller.UpdateModifierContributorSelection(parentIndex, 0, isSelected: true);
        var explicitRow = fixture.Window.FindModifier(parentIndex);
        Assert.Equal("30", explicitRow.MinimumText);
        Assert.True(explicitRow.Contributors[0].IsSelected);
        Assert.True(explicitRow.Contributors[0].IsInteractionEnabled);
        Assert.True(fixture.Window.CurrentSearchState!.CanSearch);
        Assert.True(new TradeSearchDraftValidator().Validate(fixture.Window.CurrentState!.Draft).IsValid);

        await fixture.Controller.SearchAsync();

        Assert.Equal(3, fixture.SearchClient.Calls.Count);
        using var explicitDocument = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
            fixture.SearchClient.Calls[2].Request!));
        var explicitFilters = explicitDocument.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(
            ["pseudo.local_increased_physical_damage", "explicit.stat_1509134228"],
            explicitFilters.Select(filter => filter.GetProperty("id").GetString()));
        Assert.Equal(
            [30m, 30m],
            explicitFilters.Select(filter => filter.GetProperty("value").GetProperty("min").GetDecimal()));
    }

    [Fact]
    public async Task HorrorMangler_PhysicalDpsParentAndBothPhysicalChildrenCoexistWithExactBase()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                ProviderStat(0, "explicit.stat_1509134228", "#% increased Physical Damage", "Explicit"),
                ProviderStat(1, "crafted.stat_1509134228", "#% increased Physical Damage", "Crafted"),
                ProviderStat(2, "fractured.stat_1509134228", "#% increased Physical Damage", "Fractured"),
                ProviderStat(
                    3,
                    "pseudo.pseudo_increased_physical_damage",
                    "#% total increased Physical Damage",
                    "Pseudo"),
                ProviderStat(4, "explicit.stat_803737631", "+# to Accuracy Rating", "Explicit"),
                ProviderStat(
                    5,
                    "explicit.stat_1940865751",
                    "Adds # to # Physical Damage (Local)",
                    "Explicit"),
                ProviderStat(
                    6,
                    "pseudo.pseudo_adds_physical_damage",
                    "Adds # to # Physical Damage",
                    "Pseudo"),
            ]),
            HorrorManglerWithAddedPhysical,
            expectedRarity: "Rare",
            expectedModifierCount: 3);
        var sourceDraft = fixture.MagicReaverAxeDraft;
        var exactBaseDraft = sourceDraft with
        {
            Base = sourceDraft.Base with
            {
                ActiveCriterion = Assert.IsType<BaseSearchCriterion>(
                    sourceDraft.Base.AvailableCriteria.ExactBase),
            },
        };
        var prepared = await fixture.Controller.PrepareDraftAsync(exactBaseDraft);
        var parent = Assert.Single(prepared.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        Assert.False(parent.IsSelected);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Exact, parent.ProviderResolutionStatus);
        Assert.DoesNotContain(prepared.ModifierFilters, modifier => modifier.IsSelected);
        Assert.All(
            prepared.ModifierFilters.Where(modifier =>
                modifier.OriginalText.Contains("Physical Damage", StringComparison.Ordinal)),
            modifier => Assert.DoesNotContain(
                modifier.FilterVariants,
                option => option.Label == "Pseudo"));
        fixture.Controller.UpdateCurrentDraft(
            prepared,
            new TradeSearchDraftValidator().Validate(prepared));
        var propertyRow = Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        fixture.Window.RaiseItemPropertySelectionChanged(propertyRow.SourceIndex, isSelected: true);

        await fixture.Controller.SearchAsync();

        var parentOnlyRequest = Assert.Single(fixture.SearchClient.Calls).Request!;
        using (var parentOnlyDocument = JsonDocument.Parse(
                   PathOfExileTradeJson.SerializeSearchRequest(parentOnlyRequest)))
        {
            var parentOnlyQuery = parentOnlyDocument.RootElement.GetProperty("query");
            Assert.Equal(parent.RequestedMinimum, parentOnlyQuery
                .GetProperty("filters")
                .GetProperty("weapon_filters")
                .GetProperty("filters")
                .GetProperty("pdps")
                .GetProperty("min")
                .GetDecimal());
            Assert.All(parentOnlyQuery.GetProperty("stats").EnumerateArray(), group =>
                Assert.Empty(group.GetProperty("filters").EnumerateArray()));
        }

        fixture.Window.RaiseItemPropertyExpansionChanged(propertyRow.SourceIndex, isExpanded: true);
        propertyRow = Assert.Single(fixture.Window.CurrentSearchState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);

        var increasedPhysical = Assert.Single(propertyRow.Children, modifier =>
            modifier.Text.Contains("increased Physical Damage", StringComparison.Ordinal));
        var addedPhysical = Assert.Single(propertyRow.Children, modifier =>
            modifier.Text.Contains("Adds ", StringComparison.Ordinal) &&
            modifier.Text.Contains("Physical Damage", StringComparison.Ordinal));
        fixture.Window.RaiseModifierSelectionChanged(increasedPhysical.SourceIndex, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(addedPhysical.SourceIndex, isSelected: true);

        await fixture.Controller.SearchAsync();

        Assert.Equal(2, fixture.SearchClient.Calls.Count);
        var request = fixture.SearchClient.Calls[1].Request!;
        var serialized = PathOfExileTradeJson.SerializeSearchRequest(request);
        using var document = JsonDocument.Parse(serialized);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("Reaver Axe", query.GetProperty("type").GetString());
        Assert.Equal("rare", query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString());
        Assert.False(query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .TryGetProperty("category", out _));
        Assert.Equal(parent.RequestedMinimum, query
            .GetProperty("filters")
            .GetProperty("weapon_filters")
            .GetProperty("filters")
            .GetProperty("pdps")
            .GetProperty("min")
            .GetDecimal());
        var stats = Assert.Single(query.GetProperty("stats").EnumerateArray());
        Assert.Equal("and", stats.GetProperty("type").GetString());
        Assert.Equal(
            [
                "explicit.stat_1509134228",
                "explicit.stat_1940865751",
            ],
            stats.GetProperty("filters")
                .EnumerateArray()
                .Select(filter => filter.GetProperty("id").GetString()));
        Assert.DoesNotContain("ItemPropertyContribution", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewedSemanticDescriptorId", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChaosWeapon_UnsupportedParentKeepsLocalChaosChildSelectableAndSearchable()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                ProviderStat(
                    0,
                    "explicit.stat_local_added_chaos_test",
                    "Adds # to # Chaos Damage (Local)",
                    "Explicit"),
            ]),
            ChaosWeapon,
            expectedRarity: "Rare",
            expectedModifierCount: 1);
        var prepared = await fixture.Controller.PrepareDraftAsync(fixture.MagicReaverAxeDraft);
        fixture.Controller.UpdateCurrentDraft(prepared, new TradeSearchDraftValidator().Validate(prepared));

        var chaos = Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ChaosDps);
        Assert.False(chaos.IsAvailable);
        Assert.False(chaos.CanEditBounds);
        Assert.Equal(
            "Path of Exile Trade does not expose a Chaos DPS filter.",
            chaos.AvailabilityReason);
        Assert.True(chaos.HasChildren);
        Assert.Empty(fixture.Window.CurrentSearchState.Modifiers);

        fixture.Window.RaiseItemPropertySelectionChanged(chaos.SourceIndex, isSelected: true);
        Assert.False(fixture.Window.CurrentState!.Draft.ItemProperties[chaos.SourceIndex].IsSelected);
        fixture.Window.RaiseItemPropertyExpansionChanged(chaos.SourceIndex, isExpanded: true);
        chaos = Assert.Single(fixture.Window.CurrentSearchState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ChaosDps);
        var child = Assert.Single(chaos.Children);
        Assert.Contains("Chaos Damage", child.Text, StringComparison.Ordinal);
        fixture.Window.RaiseModifierSelectionChanged(child.SourceIndex, isSelected: true);
        Assert.True(fixture.Window.CurrentState!.Draft.ModifierFilters[child.SourceIndex].IsSelected);
        Assert.True(
            fixture.Window.CurrentSearchState.CanSearch,
            $"{fixture.Window.CurrentSearchState.Status}: {fixture.Window.CurrentSearchState.Message} | " +
            string.Join(" | ", fixture.Window.CurrentState.ValidationResult.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}")));

        await fixture.Controller.SearchAsync();

        var request = Assert.Single(fixture.SearchClient.Calls).Request!;
        using var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(request));
        var query = document.RootElement.GetProperty("query");
        Assert.False(
            query.GetProperty("filters").TryGetProperty("weapon_filters", out var weaponFilters) &&
            weaponFilters.GetProperty("filters").TryGetProperty("chaos_dps", out _));
        var filters = Assert.Single(query.GetProperty("stats").EnumerateArray())
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        Assert.Equal("explicit.stat_local_added_chaos_test", Assert.Single(filters)
            .GetProperty("id")
            .GetString());
    }

    [Fact]
    public async Task HorrorMangler_ManualParentMinimumBelowSelectedChildSuspendsContributorAndEmitsParentOnly()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                ProviderStat(0, "explicit.stat_1509134228", "#% increased Physical Damage", "Explicit"),
                ProviderStat(1, "crafted.stat_1509134228", "#% increased Physical Damage", "Crafted"),
                ProviderStat(2, "fractured.stat_1509134228", "#% increased Physical Damage", "Fractured"),
                ProviderStat(3, "pseudo.local_increased_physical_damage", "#% total increased Physical Damage (Local)", "Pseudo"),
                ProviderStat(4, "explicit.stat_803737631", "+# to Accuracy Rating", "Explicit"),
            ]),
            Features.PriceChecking.PriceCheckerProductionPathCorpusTests
                .HorrorManglerExplicitAndCraftedPhysicalDamageText,
            expectedRarity: "Rare",
            expectedModifierCount: 2);
        var prepared = await fixture.Controller.PrepareDraftAsync(fixture.MagicReaverAxeDraft);
        var parentIndex = prepared.ModifierFilters
            .Select((modifier, index) => new { Modifier = modifier, Index = index })
            .Single(entry => entry.Modifier.OriginalText.Contains(
                "increased Physical Damage",
                StringComparison.Ordinal))
            .Index;

        fixture.Controller.UpdateCurrentDraft(prepared, new TradeSearchDraftValidator().Validate(prepared));
        var initial = fixture.Window.FindModifier(parentIndex);
        Assert.Equal("146", initial.MinimumText);
        Assert.All(initial.Contributors, contributor => Assert.False(contributor.IsSelected));

        fixture.Controller.UpdateModifierBounds(parentIndex, "100", string.Empty);
        fixture.Controller.UpdateModifierExpansion(parentIndex, isExpanded: true);
        fixture.Controller.UpdateModifierExpansion(parentIndex, isExpanded: false);
        Assert.Equal("100", fixture.Window.FindModifier(parentIndex).MinimumText);

        fixture.Controller.UpdateModifierContributorSelection(parentIndex, 1, isSelected: true);
        var childSelected = fixture.Window.FindModifier(parentIndex);
        Assert.Equal("116", childSelected.MinimumText);
        Assert.True(childSelected.Contributors[1].IsSelected);
        Assert.False(childSelected.Contributors[1].IsInactive);

        fixture.Controller.UpdateModifierBounds(parentIndex, "100", string.Empty);
        childSelected = fixture.Window.FindModifier(parentIndex);
        Assert.Equal("100", childSelected.MinimumText);
        Assert.True(childSelected.Contributors[1].IsInactive);
        Assert.Equal(
            SearchComponentContributorInactiveReason.ParentBoundBelowSelectedChildFloor,
            childSelected.Contributors[1].InactiveReason);
        Assert.False(childSelected.Contributors[1].CanEditBounds);

        fixture.Controller.UpdateModifierBounds(parentIndex, "116", string.Empty);
        var reactivated = fixture.Window.FindModifier(parentIndex);
        Assert.Equal("116", reactivated.MinimumText);
        Assert.True(reactivated.Contributors[1].IsSelected);
        Assert.False(reactivated.Contributors[1].IsInactive);

        fixture.Controller.UpdateModifierBounds(parentIndex, "100", string.Empty);
        await fixture.Controller.SearchAsync();

        using var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
            Assert.Single(fixture.SearchClient.Calls).Request!));
        var filters = document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        var parentFilter = Assert.Single(filters);
        Assert.Equal("pseudo.local_increased_physical_damage", parentFilter.GetProperty("id").GetString());
        Assert.Equal(100m, parentFilter.GetProperty("value").GetProperty("min").GetDecimal());

        fixture.Controller.UpdateModifierContributorSelection(parentIndex, 1, isSelected: false);
        Assert.False(fixture.Window.FindModifier(parentIndex).Contributors[1].IsSelected);
    }

    [Fact]
    public async Task MixedAttackSpeedAggregate_RetainsAndSearchesCraftedContributorIdentityGenerically()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                ProviderStat(0, "explicit.stat_210067635", "#% increased Attack Speed (Local)", "Explicit"),
                ProviderStat(1, "crafted.stat_210067635", "#% increased Attack Speed (Local)", "Crafted"),
                ProviderStat(2, "pseudo.local_attack_speed", "+#% total Attack Speed (Local)", "Pseudo"),
            ]),
            MixedExplicitAndCraftedAttackSpeedText,
            expectedRarity: "Rare",
            expectedModifierCount: 1);
        var prepared = await fixture.Controller.PrepareDraftAsync(fixture.MagicReaverAxeDraft);
        var aggregate = Assert.Single(prepared.ModifierFilters);

        Assert.Equal("47% increased Attack Speed", aggregate.OriginalText);
        Assert.Equal(["Explicit", "Crafted"], aggregate.Sources.Select(source => source.ProviderDomain));
        Assert.Equal(
            [
                PathOfExileTradeProviderIdentity.Create("explicit.stat_210067635"),
                PathOfExileTradeProviderIdentity.Create("crafted.stat_210067635"),
            ],
            aggregate.Contributors.Select(contributor => contributor.ProviderIdentity));
        Assert.All(aggregate.Contributors, contributor =>
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, contributor.ProviderResolutionStatus));
        Assert.Equal("Pseudo", Assert.Single(aggregate.FilterVariants, option =>
            option.Identity == aggregate.SelectedFilterVariantIdentity).Label);

        fixture.Controller.UpdateCurrentDraft(prepared, new TradeSearchDraftValidator().Validate(prepared));
        fixture.Controller.UpdateModifierContributorSelection(0, 1, isSelected: true);
        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.SearchClient.Calls);
        using var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(call.Request!));
        var filters = document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(
            ["pseudo.local_attack_speed", "crafted.stat_210067635"],
            filters.Select(filter => filter.GetProperty("id").GetString()));
        Assert.Equal(
            [20m, 20m],
            filters.Select(filter => filter.GetProperty("value").GetProperty("min").GetDecimal()));
    }

    [Fact]
    public async Task MorbidBite_LocalPhysicalAndAttackSpeedRejectBroadPseudoVariants()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                ProviderStat(0, "explicit.stat_1940865751", "Adds # to # Physical Damage (Local)", "Explicit"),
                ProviderStat(1, "pseudo.pseudo_adds_physical_damage", "Adds # to # Physical Damage", "Pseudo"),
                ProviderStat(2, "explicit.stat_210067635", "#% increased Attack Speed (Local)", "Explicit"),
                ProviderStat(3, "explicit.stat_681332047", "#% increased Attack Speed", "Explicit"),
                ProviderStat(4, "pseudo.pseudo_total_attack_speed", "+#% total Attack Speed", "Pseudo"),
            ]),
            LoadAdvancedCorpusItem("Morbid Bite"),
            expectedRarity: "Rare",
            expectedModifierCount: 2);

        var prepared = await fixture.Controller.PrepareDraftAsync(fixture.MagicReaverAxeDraft);

        Assert.Equal(
            ["explicit.stat_1940865751", "explicit.stat_210067635"],
            prepared.ModifierFilters.Select(modifier => modifier.ProviderStatId));
        Assert.All(prepared.ModifierFilters, modifier => Assert.DoesNotContain(
            modifier.FilterVariants,
            option => option.Label == "Pseudo"));
        Assert.DoesNotContain(prepared.ModifierFilters.SelectMany(modifier => modifier.FilterVariants), option =>
            option.Description.Contains("Attack Speed", StringComparison.Ordinal) &&
            !option.Description.Contains("(Local)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WrathCry_LocalColdDamageRejectsBroadPseudoAndRetainsOfficialLocalExplicit()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                ProviderStat(0, "explicit.stat_1037193709", "Adds # to # Cold Damage (Local)", "Explicit"),
                ProviderStat(1, "pseudo.pseudo_adds_cold_damage", "Adds # to # Cold Damage", "Pseudo"),
            ]),
            LoadAdvancedCorpusItem("Wrath Cry"),
            expectedRarity: "Rare",
            expectedModifierCount: 6,
            expectedBaseName: "Blasting Wand",
            expectedCategory: "Wand");

        var prepared = await fixture.Controller.PrepareDraftAsync(fixture.MagicReaverAxeDraft);
        var cold = Assert.Single(prepared.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Cold Damage", StringComparison.Ordinal));

        Assert.Equal("explicit.stat_1037193709", cold.ProviderStatId);
        Assert.DoesNotContain(cold.FilterVariants, option => option.Label == "Pseudo");
        Assert.Contains(cold.FilterVariants, option => option.Label == "Explicit");
    }

    [Fact]
    public async Task PriceCheckerProductionPath_LocalAddedColdTupleUsesOfficialMeanBoundInJsonAndTradeUrl()
    {
        var fixture = ProductionTradeCategoryFixture.CreateForItem(
            new PathOfExileTradeStatCatalog(
            [
                Stat(
                    0,
                    "explicit.stat_1037193709",
                    "Adds # to # Cold Damage (Local)"),
            ]),
            MagicReaverAxeWithLocalColdDamageText,
            expectedRarity: "Magic",
            expectedModifierCount: 1);
        var preparedDraft = await fixture.Controller.PrepareDraftAsync(fixture.MagicReaverAxeDraft);
        fixture.Controller.UpdateCurrentDraft(
            preparedDraft,
            new TradeSearchDraftValidator().Validate(preparedDraft));
        var row = fixture.Window.FindModifier(0);

        Assert.True(row.SupportsValueBounds, row.ValueBoundsUnsupportedReason);
        Assert.Equal("19.5", row.MinimumText);
        Assert.Empty(row.MaximumText);
        fixture.Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: true);

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
        var call = Assert.Single(fixture.SearchClient.Calls);
        using var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(call.Request!));
        var filter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("explicit.stat_1037193709", filter.GetProperty("id").GetString());
        Assert.Equal(19.5m, filter.GetProperty("value").GetProperty("min").GetDecimal());
        Assert.Equal(JsonValueKind.Number, filter.GetProperty("value").GetProperty("min").ValueKind);

        fixture.Window.RaiseTradeRequested();

        Assert.Equal(
            "https://www.pathofexile.com/trade/search/Mirage/query-1",
            Assert.Single(fixture.UrlLauncher.OpenedUrls));
        Assert.Single(fixture.SearchClient.Calls);
    }

    [Fact]
    public void TradeCategoryCatalog_OneHandAxesResolvesProviderOptionIndependentOfCasingAndOrdering()
    {
        var catalog = new PathOfExileTradeFilterCatalog(
        [
            Category(0, "armour.shield", "Shield"),
            Category(1, "weapon.oneaxe", "One-Handed Axe"),
            Category(2, "weapon.bow", "Bow"),
        ]);

        Assert.True(catalog.TryFindCategoryOption("one hand axes", out var pluralOption));
        Assert.Equal("weapon.oneaxe", pluralOption.Id);

        Assert.True(catalog.TryFindCategoryOption("ONE HAND AXE", out var singularOption));
        Assert.Equal("weapon.oneaxe", singularOption.Id);
    }

    [Fact]
    public void TradeCategoryCatalog_ExistingOrdinaryCategoriesRemainProviderMapped()
    {
        var catalog = new PathOfExileTradeFilterCatalog(
        [
            Category(0, "weapon.bow", "Bow"),
            Category(1, "armour.shield", "Shield"),
            Category(2, "armour.chest", "Body Armour"),
            Category(3, "accessory.ring", "Ring"),
            Category(4, "weapon.wand", "Wand"),
            Category(5, "jewel.base", "Base Jewel"),
        ]);

        Assert.Equal("weapon.bow", Find(catalog, "Bow").Id);
        Assert.Equal("armour.shield", Find(catalog, "Shield").Id);
        Assert.Equal("armour.chest", Find(catalog, "Body Armour").Id);
        Assert.Equal("accessory.ring", Find(catalog, "Ring").Id);
        Assert.Equal("weapon.wand", Find(catalog, "Wand").Id);
        Assert.Equal("jewel.base", Find(catalog, "Jewel").Id);
    }

    [Theory]
    [InlineData("Wand", "Wand")]
    [InlineData("One Hand Axes", "One-Handed Axe")]
    [InlineData("Belt", "Belt")]
    public void TradeCategoryCatalog_DisplayLabelUsesOfficialProviderOptionText(
        string category,
        string expectedDisplayLabel)
    {
        var catalog = new PathOfExileTradeFilterCatalog(
        [
            Category(0, "weapon.wand", "Wand"),
            Category(1, "weapon.oneaxe", "One-Handed Axe"),
            Category(2, "accessory.belt", "Belt"),
        ]);

        Assert.True(catalog.TryGetCategoryDisplayLabel(category, out var displayLabel));
        Assert.Equal(expectedDisplayLabel, displayLabel);
    }

    [Fact]
    public void TradeCategoryCatalog_UnknownCategoryStillFailsExplicitly()
    {
        var catalog = new PathOfExileTradeFilterCatalog([Category(0, "weapon.bow", "Bow")]);

        Assert.False(catalog.TryFindCategoryOption("Unknown Category", out _));
    }

    private static PathOfExileTradeFilterOption Find(
        PathOfExileTradeFilterCatalog catalog,
        string category)
    {
        Assert.True(catalog.TryFindCategoryOption(category, out var option));
        return option;
    }

    private static PathOfExileTradeFilterOption Category(
        int order,
        string id,
        string text)
    {
        return new PathOfExileTradeFilterOption
        {
            ProviderOrder = order,
            GroupId = "type_filters",
            FilterId = "category",
            Id = id,
            Text = text,
        };
    }

    private sealed class ProductionTradeCategoryFixture
    {
        private ProductionTradeCategoryFixture(
            TradeSearchDraft magicReaverAxeDraft,
            TradeSearchValidationResult magicReaverAxeValidation,
            FakeWindow window,
            PriceCheckerSearchController controller,
            FakeSearchClient searchClient,
            FakeExternalUrlLauncher urlLauncher)
        {
            MagicReaverAxeDraft = magicReaverAxeDraft;
            MagicReaverAxeValidation = magicReaverAxeValidation;
            Window = window;
            Controller = controller;
            SearchClient = searchClient;
            UrlLauncher = urlLauncher;
        }

        public TradeSearchDraft MagicReaverAxeDraft { get; }

        public TradeSearchValidationResult MagicReaverAxeValidation { get; }

        public FakeWindow Window { get; }

        public PriceCheckerSearchController Controller { get; }

        public FakeSearchClient SearchClient { get; }

        public FakeExternalUrlLauncher UrlLauncher { get; }

        public static ProductionTradeCategoryFixture Create(PathOfExileTradeStatCatalog statCatalog)
        {
            return CreateForItem(statCatalog, MagicReaverAxeText, "Magic", expectedModifierCount: 1);
        }

        public static ProductionTradeCategoryFixture CreateForItem(
            PathOfExileTradeStatCatalog statCatalog,
            string itemText,
            string expectedRarity,
            int expectedModifierCount,
            string expectedBaseName = "Reaver Axe",
            string expectedCategory = "One Hand Axe")
        {
            var catalog = LoadGameDataCatalog();
            var parsed = new ItemTextParser().Parse(itemText);
            var displayService = new ParsedItemGameDataDisplayService();
            var baseResolution = displayService.ResolveItemBase(parsed, catalog).Result;
            Assert.NotNull(baseResolution);
            Assert.Equal(expectedBaseName, baseResolution.ResolvedBaseName);

            var modifierResolutions = displayService
                .ResolveModifierCandidates(parsed, catalog, baseResolution)
                .Results
                .Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();
            var draftResult = new TradeSearchDraftMapper().CreateDraft(
                parsed,
                baseResolution,
                modifierResolutions,
                catalog);
            Assert.True(draftResult.IsSuccess);
            Assert.NotNull(draftResult.Draft);
            Assert.Equal(expectedRarity, draftResult.Draft!.Rarity);
            Assert.Equal(expectedCategory, draftResult.Draft.Base.ActiveCriterion?.Category);
            Assert.Equal(BaseSearchMode.Category, draftResult.Draft.Base.ActiveCriterion?.Mode);
            Assert.Equal(expectedModifierCount, draftResult.Draft.ModifierFilters.Count);

            var searchClient = new FakeSearchClient();
            var service = new PathOfExileTradePriceCheckService(
                new PathOfExileTradeQueryBuilder(),
                new PathOfExileTradeStatMatcher(),
                new FakeStatCatalogProvider(statCatalog),
                new FakeItemCatalogProvider(),
                new PathOfExileTradeSelectedModifierMapper(),
                new FakeItemIdentityMapper(),
                searchClient,
                new FakeFetchClient(),
                new FakeFilterCatalogProvider(OneHandAxeFilterCatalog()));
            var window = new FakeWindow();
            var urlLauncher = new FakeExternalUrlLauncher();
            var controller = new PriceCheckerSearchController(
                service,
                externalUrlLauncher: urlLauncher);
            controller.AttachWindow(window);

            return new ProductionTradeCategoryFixture(
                draftResult.Draft,
                new TradeSearchDraftValidator().Validate(draftResult.Draft),
                window,
                controller,
                searchClient,
                urlLauncher);
        }
    }

    private static PathOfExileTradeFilterCatalog OneHandAxeFilterCatalog()
    {
        return PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    }

    private static PathOfExileTradeStatCatalog AttackSpeedStatCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            Stat(0, "explicit.stat_681332047", "#% increased Attack Speed"),
            Stat(1, "explicit.stat_210067635", "#% increased Attack Speed (Local)"),
            PseudoStat(2, "pseudo.pseudo_total_attack_speed", "+#% total Attack Speed"),
        ]);
    }

    private static PriceCheckerModifierViewModel ExpandAttackSpeedChild(ProductionTradeCategoryFixture fixture)
    {
        var source = fixture.MagicReaverAxeDraft.ModifierFilters
            .Select((modifier, index) => new { Modifier = modifier, Index = index })
            .Single(entry => entry.Modifier.OriginalText.Contains(
                "increased Attack Speed",
                StringComparison.Ordinal));
        var property = Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties, candidate =>
            candidate.Kind == TradeSearchItemPropertyKind.AttacksPerSecond);
        Assert.False(property.IsSelected);

        fixture.Window.RaiseItemPropertyExpansionChanged(property.SourceIndex, isExpanded: true);

        var child = Assert.Single(
            fixture.Window.CurrentSearchState!.ItemProperties
                .Single(candidate => candidate.Kind == TradeSearchItemPropertyKind.AttacksPerSecond)
                .Children,
            candidate => candidate.SourceIndex == source.Index);
        Assert.DoesNotContain(fixture.Window.CurrentSearchState.Modifiers, candidate =>
            candidate.SourceIndex == child.SourceIndex);
        Assert.Equal(
            1,
            fixture.Window.CurrentSearchState.Modifiers
                .Concat(fixture.Window.CurrentSearchState.ItemProperties.SelectMany(candidate => candidate.Children))
                .Count(candidate => candidate.SourceIndex == child.SourceIndex));
        return child;
    }

    private static PathOfExileTradeStatEntry Stat(
        int order,
        string id,
        string text)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = "explicit",
            GroupLabel = "Explicit",
            Id = id,
            Text = text,
            Type = "explicit",
        };
    }

    private static PathOfExileTradeStatEntry CraftedStat(
        int order,
        string id,
        string text)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = "crafted",
            GroupLabel = "Crafted",
            Id = id,
            Text = text,
            Type = "crafted",
        };
    }

    private static PathOfExileTradeStatEntry PseudoStat(
        int order,
        string id,
        string text)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = "pseudo",
            GroupLabel = "Pseudo",
            Id = id,
            Text = text,
            Type = "pseudo",
        };
    }

    private static PathOfExileTradeStatEntry ProviderStat(
        int order,
        string id,
        string text,
        string kind)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = kind.ToLowerInvariant(),
            GroupLabel = kind,
            Id = id,
            Text = text,
            Type = kind.ToLowerInvariant(),
        };
    }

    private const string MagicReaverAxeText = """
Item Class: One Hand Axes
Rarity: Magic
Reaver Axe of Celebration
--------
One Handed Axe
Physical Damage: 38-114
Critical Strike Chance: 5.00%
Attacks per Second: 1.51 (augmented)
Weapon Range: 1.1 metres
--------
Requirements:
Level: 61
Str: 167
Dex: 57
--------
Sockets: B B-R
--------
Item Level: 85
--------
{ Suffix Modifier "of Celebration" (Tier: 1) - Attack, Speed }
26(26-27)% increased Attack Speed
""";

    internal const string ArmageddonThirstCraftedAttackSpeedText = """
Item Class: One Hand Axes
Rarity: Rare
Armageddon Thirst
Reaver Axe
--------
One Handed Axe
Physical Damage: 38-114
Critical Strike Chance: 5.00%
Attacks per Second: 1.50 (augmented)
Weapon Range: 1.1 metres
--------
Item Level: 85
--------
{ Suffix Modifier "of Thirst" (Tier: 1) - Attack, Physical, Mana }
2.83(2.6-3.2)% of Physical Attack Damage Leeched as Mana
{ Master Crafted Suffix Modifier "of Craft" (Rank: 3) - Attack, Speed }
20(16-20)% increased Attack Speed
""";

    private const string MixedExplicitAndCraftedAttackSpeedText = """
Item Class: One Hand Axes
Rarity: Rare
Tempest Edge
Reaver Axe
--------
One Handed Axe
Physical Damage: 38-114
Critical Strike Chance: 5.00%
Attacks per Second: 1.70 (augmented)
Weapon Range: 1.1 metres
--------
Item Level: 85
--------
{ Suffix Modifier "of Celebration" (Tier: 1) - Attack, Speed }
27(26-27)% increased Attack Speed
{ Master Crafted Suffix Modifier "of Craft" (Rank: 3) - Attack, Speed }
20(16-20)% increased Attack Speed
""";

    private const string HorrorManglerWithAddedPhysical = """
Item Class: One Hand Axes
Rarity: Rare
Horror Mangler
Reaver Axe
--------
One Handed Axe
Physical Damage: 94-283 (augmented)
Critical Strike Chance: 5.00%
Attacks per Second: 1.30
--------
Item Level: 85
--------
{ Prefix Modifier "Reaver's" (Tier: 3) - Damage, Physical, Attack }
30(25-34)% increased Physical Damage
+60(47-72) to Accuracy Rating
{ Master Crafted Prefix Modifier "Upgraded" (Rank: 4) - Damage, Physical, Attack }
116(100-129)% increased Physical Damage
{ Prefix Modifier "Flaring" (Tier: 1) - Damage, Physical, Attack }
Adds 23(22-29) to 46(45-52) Physical Damage
""";

    private const string ChaosWeapon = """
Item Class: One Hand Axes
Rarity: Rare
Chaos Edge
Reaver Axe
--------
One Handed Axe
Chaos Damage: 10-20 (augmented)
Attacks per Second: 1.00
--------
Item Level: 85
--------
{ Prefix Modifier "Malicious" (Tier: 1) - Damage, Chaos, Attack }
Adds 60(56-87) to 120(105-160) Chaos Damage
""";

    private const string MagicReaverAxeWithLocalColdDamageText = """
Item Class: One Hand Axes
Rarity: Magic
Icy Reaver Axe
--------
One Handed Axe
Physical Damage: 38-114
Elemental Damage: 14-25 (augmented)
Critical Strike Chance: 5.00%
Attacks per Second: 1.30
Weapon Range: 1.1 metres
--------
Requirements:
Level: 61
Str: 167
Dex: 57
--------
Item Level: 85
--------
{ Prefix Modifier "Icy" (Tier: 1) - Attack, Elemental, Cold, Damage }
Adds 14(11-15) to 25(23-26) Cold Damage
""";


    private static GameDataCatalog LoadGameDataCatalog()
    {
        var packagePath = FindRepoFile("artifacts", "poenhance-game-data.json");
        var result = GameDataPackageLoader
            .LoadFromFileAsync(packagePath)
            .GetAwaiter()
            .GetResult();

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
        Assert.NotNull(result.Package);
        return GameDataCatalog.FromPackage(result.Package!);
    }

    private static string LoadAdvancedCorpusItem(string displayName)
    {
        var corpus = File.ReadAllText(FindRepoFile(
            "PoEnhance.Core.Tests",
            "TestData",
            "Items",
            "advanced-real-items-corpus.txt"));
        return Assert.Single(Regex.Split(
            corpus.Trim(),
            @"\r?\n\s*\r?\n(?=Item Class:)",
            RegexOptions.CultureInvariant), item => item.ReplaceLineEndings("\n").Contains(
                $"\n{displayName}\n",
                StringComparison.Ordinal));
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file: {Path.Combine(relativeParts)}");
    }

    private sealed record SearchCall(
        PathOfExileTradeSearchRequest? Request,
        string? LeagueIdentifier);

    private sealed class FakeStatCatalogProvider : IPathOfExileTradeStatCatalogProvider
    {
        private readonly PathOfExileTradeStatCatalog catalog;

        public FakeStatCatalogProvider(PathOfExileTradeStatCatalog catalog)
        {
            this.catalog = catalog;
        }

        public bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
        }
    }

    private sealed class FakeFilterCatalogProvider : IPathOfExileTradeFilterCatalogProvider
    {
        private readonly PathOfExileTradeFilterCatalog catalog;

        public FakeFilterCatalogProvider(PathOfExileTradeFilterCatalog catalog)
        {
            this.catalog = catalog;
        }

        public Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PathOfExileTradeFilterCatalogProviderResult.Success(catalog));
        }
    }

    private sealed class FakeSearchClient : IPathOfExileTradeSearchClient
    {
        public List<SearchCall> Calls { get; } = [];

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new SearchCall(request, leagueIdentifier));
            return Task.FromResult(new PathOfExileTradeSearchExecutionResult
            {
                IsSuccess = true,
                Response = new PathOfExileTradeSearchResponse
                {
                    Id = "query-1",
                    Result = [],
                    Total = 0,
                },
            });
        }
    }

    private sealed class FakeFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Fetch is not expected for zero-result fake Search responses.");
        }
    }

    private sealed class FakeExternalUrlLauncher : IExternalUrlLauncher
    {
        public List<string> OpenedUrls { get; } = [];

        public bool TryOpen(Uri uri)
        {
            OpenedUrls.Add(uri.AbsoluteUri);
            return true;
        }
    }

    private sealed class FakeItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Item catalog is not expected for ordinary category searches.");
        }
    }

    private sealed class FakeItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
    {
        public PathOfExileTradeItemIdentityMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeItemCatalog? catalog)
        {
            throw new InvalidOperationException("Item identity mapping is not expected for ordinary category searches.");
        }
    }

#pragma warning disable CS0067
    private sealed class FakeWindow : IPriceCheckerWindow
    {
        public event EventHandler? Closed;
        public event EventHandler? PanelActivated;
        public event EventHandler? PanelDeactivated;
        public event EventHandler? PanelInteraction;
        public event EventHandler? SearchRequested;

        public event EventHandler? LoadMoreRequested;

        public event EventHandler? TradeRequested;

        public event EventHandler<PriceCheckerOfferCapacityChangedEventArgs>? OfferCapacityChanged;
        public event EventHandler<PriceCheckerItemPropertySelectionChangedEventArgs>? ItemPropertySelectionChanged;
        public event EventHandler<PriceCheckerItemPropertyBoundsChangedEventArgs>? ItemPropertyBoundsChanged;
        public event EventHandler<PriceCheckerItemPropertyExpansionChangedEventArgs>? ItemPropertyExpansionChanged;
        public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;

        public event EventHandler<PriceCheckerModifierBoundsChangedEventArgs>? ModifierBoundsChanged;

        public event EventHandler<PriceCheckerModifierFilterVariantChangedEventArgs>? ModifierFilterVariantChanged;

        public event EventHandler<PriceCheckerModifierExpansionChangedEventArgs>? ModifierExpansionChanged;

        public event EventHandler? BaseCriterionToggleRequested;
        public event EventHandler<bool>? PinStateChanged;
        public event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;
        public event EventHandler? HorizontalDragCompleted;
        public event EventHandler? HorizontalResizeStarted;
        public event EventHandler<PriceCheckerHorizontalResizeEventArgs>? HorizontalResizeDelta;
        public event EventHandler? HorizontalResizeCompleted;
        public event EventHandler? ResetItemRequested;

        public bool IsClosed { get; private set; }

        public bool IsPinned { get; private set; }

        public PriceCheckerWindowState? CurrentState { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public PriceCheckerSearchViewState? CurrentSearchState { get; private set; }

        public PriceCheckerPlacement? GetDisplayedPlacement() => CurrentPlacement;

        public void UpdateContent(PriceCheckerWindowState state)
        {
            CurrentState = state;
        }

        public void UpdateSearch(PriceCheckerSearchViewState state)
        {
            CurrentSearchState = state;
        }

        public void ApplyPlacement(PriceCheckerPlacement placement)
        {
            CurrentPlacement = placement;
        }

        public void ShowInactive()
        {
        }

        public void Close()
        {
            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseModifierSelectionChanged(int modifierIndex, bool isSelected)
        {
            ModifierSelectionChanged?.Invoke(
                this,
                new PriceCheckerModifierSelectionChangedEventArgs(modifierIndex, isSelected));
        }

        public void RaiseItemPropertySelectionChanged(int propertyIndex, bool isSelected)
        {
            ItemPropertySelectionChanged?.Invoke(
                this,
                new PriceCheckerItemPropertySelectionChangedEventArgs(propertyIndex, isSelected));
        }

        public void RaiseItemPropertyExpansionChanged(int propertyIndex, bool isExpanded)
        {
            ItemPropertyExpansionChanged?.Invoke(
                this,
                new PriceCheckerItemPropertyExpansionChangedEventArgs(propertyIndex, isExpanded));
        }

        public void RaiseModifierFilterVariantChanged(int modifierIndex, string variantIdentity)
        {
            ModifierFilterVariantChanged?.Invoke(
                this,
                new PriceCheckerModifierFilterVariantChangedEventArgs(modifierIndex, variantIdentity));
        }

        public PriceCheckerModifierViewModel FindModifier(int modifierIndex)
        {
            Assert.NotNull(CurrentSearchState);
            var match = CurrentSearchState!.Modifiers
                .Concat(CurrentSearchState.ItemProperties.SelectMany(property => property.Children))
                .SingleOrDefault(modifier => modifier.SourceIndex == modifierIndex);
            if (match is not null)
            {
                return match;
            }

            Assert.NotNull(CurrentState);
            var group = Assert.Single(CurrentState!.Draft.ItemPropertyContributionGroups, candidate =>
                candidate.Contributions.Any(contribution =>
                    contribution.ModifierFilterIndex == modifierIndex));
            var property = Assert.Single(CurrentSearchState.ItemProperties, candidate =>
                candidate.Kind == group.ParentKind);
            RaiseItemPropertyExpansionChanged(property.SourceIndex, isExpanded: true);
            return Assert.Single(
                CurrentSearchState.ItemProperties.SelectMany(candidate => candidate.Children),
                modifier => modifier.SourceIndex == modifierIndex);
        }

        public void RaiseTradeRequested()
        {
            TradeRequested?.Invoke(this, EventArgs.Empty);
        }
    }
#pragma warning restore CS0067
}

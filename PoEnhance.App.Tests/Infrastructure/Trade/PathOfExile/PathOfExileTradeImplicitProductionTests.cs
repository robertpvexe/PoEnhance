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

public sealed class PathOfExileTradeImplicitProductionTests
{
    [Fact]
    public async Task BlastingWandImplicitSelectedCreatesOneImplicitProviderFilter()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "explicit.caster", "Cannot roll Caster Modifiers", "explicit"),
            Stat(1, "implicit.caster", "Cannot roll Caster Modifiers", "implicit")));
        fixture.OpenText(CorpusItem(4));
        fixture.SelectRow("Cannot roll Caster Modifiers");

        await fixture.SearchAsync();

        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("rare", Rarity(query));
        Assert.Equal("weapon.wand", Category(query));
        Assert.Equal("securable", query.GetProperty("status").GetProperty("option").GetString());
        Assert.False(query.TryGetProperty("type", out _));
        Assert.Equal(["implicit.caster"], StatIds(query));
        Assert.Equal(fixture.Window.CurrentSearchState!.SelectedModifierCount, StatIds(query).Length);
    }

    [Fact]
    public async Task SupremeSpikedShieldImplicitSelectedCreatesOneImplicitProviderFilterAndAggregatesStunRecoveryRows()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "explicit.suppress", "+#% chance to Suppress Spell Damage", "explicit"),
            Stat(1, "implicit.suppress", "+#% chance to Suppress Spell Damage", "implicit")));
        fixture.OpenText(SupremeSpikedShieldText);
        fixture.SelectRow("chance to Suppress Spell Damage");

        await fixture.SearchAsync();

        Assert.Equal(2, fixture.Window.CurrentSearchState!.Modifiers.Count);
        var recovery = Assert.Single(fixture.Window.CurrentSearchState.Modifiers, modifier =>
            modifier.Text == "24% increased Stun and Block Recovery");
        Assert.Equal(2, recovery.Contributors.Count);
        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("magic", Rarity(query));
        Assert.Equal("armour.shield", Category(query));
        Assert.Equal(["implicit.suppress"], StatIds(query));
    }

    [Fact]
    public async Task OrganicRingEachImplicitSelectedAloneCreatesOneImplicitProviderFilter()
    {
        var catalog = OrganicCatalog();
        foreach (var testCase in new[]
        {
            ("additional Physical Damage Reduction", "implicit.phys_reduction"),
            ("Cannot roll Modifiers of Non-Physical Damage Types", "implicit.non_phys"),
        })
        {
            var fixture = Fixture.Create(catalog);
            fixture.OpenText(CorpusItem(7));
            fixture.SelectRow(testCase.Item1);

            await fixture.SearchAsync();

            var json = fixture.SingleSearchJson();
            using var document = JsonDocument.Parse(json);
            var query = document.RootElement.GetProperty("query");
            Assert.Equal("rare", Rarity(query));
            Assert.Equal("accessory.ring", Category(query));
            Assert.Equal([testCase.Item2], StatIds(query));
        }
    }

    [Fact]
    public async Task OrganicRingBothImplicitsSelectedCreateTwoDistinctAndFilters()
    {
        var fixture = Fixture.Create(OrganicCatalog());
        fixture.OpenText(CorpusItem(7));
        fixture.SelectRow("additional Physical Damage Reduction");
        fixture.SelectRow("Cannot roll Modifiers of Non-Physical Damage Types");

        await fixture.SearchAsync();

        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        var statsGroup = Assert.Single(query.GetProperty("stats").EnumerateArray());
        Assert.Equal("and", statsGroup.GetProperty("type").GetString());
        var ids = StatIds(query);
        Assert.Equal(["implicit.phys_reduction", "implicit.non_phys"], ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(fixture.Window.CurrentSearchState!.SelectedModifierCount, ids.Length);
    }

    [Fact]
    public async Task StygianViseSelectedAbyssalSocketUsesExactBaseFallbackWithoutStatFilter()
    {
        var fixture = Fixture.Create(Catalog());
        fixture.OpenText(CorpusItem(10));
        fixture.SelectRow("Has 1 Abyssal Socket");

        var selectedState = Assert.IsType<PriceCheckerWindowState>(fixture.Window.CurrentState);
        var selectedComponent = Assert.Single(selectedState.Draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Has 1 Abyssal Socket", StringComparison.Ordinal));
        Assert.False(selectedComponent.SupportsValueBounds);
        Assert.Equal("Stygian Vise", selectedComponent.GuaranteedExactBaseName);
        Assert.Equal(BaseSearchMode.ExactBase, selectedState.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", selectedState.Draft.Base.ActiveCriterion?.ExactBaseName);
        Assert.Empty(fixture.SearchClient.Calls);

        await fixture.SearchAsync();

        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("rare", Rarity(query));
        Assert.Equal("Stygian Vise", query.GetProperty("type").GetString());
        Assert.False(query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .TryGetProperty("category", out _));
        Assert.Empty(StatIds(query));

        var state = Assert.IsType<PriceCheckerWindowState>(fixture.Window.CurrentState);
        Assert.Equal(BaseSearchMode.ExactBase, state.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", state.Draft.Base.ActiveCriterion?.ExactBaseName);
        var component = Assert.Single(state.Draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Has 1 Abyssal Socket", StringComparison.Ordinal));
        Assert.True(component.IsSelected);
        Assert.Equal(SearchComponentProviderResolutionStatus.BaseGuaranteed, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.DoesNotContain(state.ValidationResult.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved);
        Assert.Contains(state.ValidationResult.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierRepresentedByExactBase &&
            diagnostic.Severity == TradeSearchValidationSeverity.Info);
    }

    [Fact]
    public void StygianViseDeselectingAbyssalSocketRestoresCategoryWithoutInventingAnotherExactBase()
    {
        var fixture = Fixture.Create(Catalog());
        fixture.OpenText(CorpusItem(10));
        var row = fixture.SelectRow("Has 1 Abyssal Socket");

        fixture.Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: false);

        var state = Assert.IsType<PriceCheckerWindowState>(fixture.Window.CurrentState);
        Assert.Equal(BaseSearchMode.Category, state.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal("Belt", state.Draft.Base.ActiveCriterion?.Category);
        Assert.Null(state.Draft.Base.ActiveCriterion?.ExactBaseName);
        Assert.Empty(fixture.SearchClient.Calls);
    }

    [Theory]
    [InlineData(9, 22, 31)]
    [InlineData(10, 25, 35)]
    public async Task CataclysmLeagueSameIdentityExplicitChildrenComposeIntoOneParentFilter(
        int firstMinimum,
        int secondMinimum,
        int expectedMinimum)
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "explicit.stun-recovery", "#% increased Stun and Block Recovery", "explicit")));
        fixture.OpenText(CataclysmLeagueSlinkBootsText);
        var row = Assert.Single(fixture.Window.CurrentSearchState!.Modifiers, modifier =>
            modifier.Text.Contains("Stun and Block Recovery", StringComparison.Ordinal));

        Assert.Equal("Explicit", Assert.Single(row.FilterVariants).Label);
        Assert.All(row.Contributors, contributor => Assert.True(contributor.IsInteractionEnabled));
        fixture.Window.RaiseModifierContributorSelectionChanged(row.SourceIndex, 0, isSelected: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(row.SourceIndex, 1, isSelected: true);
        if (firstMinimum != 9 || secondMinimum != 22)
        {
            fixture.Window.RaiseModifierContributorBoundsChanged(row.SourceIndex, 0, firstMinimum.ToString(), string.Empty);
            fixture.Window.RaiseModifierContributorBoundsChanged(row.SourceIndex, 1, secondMinimum.ToString(), string.Empty);
        }

        row = Assert.Single(fixture.Window.CurrentSearchState!.Modifiers, modifier =>
            modifier.Text.Contains("Stun and Block Recovery", StringComparison.Ordinal));
        Assert.Equal(expectedMinimum.ToString(), row.MinimumText);
        Assert.Equal(2, row.ActiveContributorCount);

        await fixture.SearchAsync();

        using var document = JsonDocument.Parse(fixture.SingleSearchJson());
        var query = document.RootElement.GetProperty("query");
        var filter = Assert.Single(query.GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stun-recovery", filter.GetProperty("id").GetString());
        Assert.Equal(expectedMinimum, filter.GetProperty("value").GetProperty("min").GetInt32());
    }

    [Fact]
    public async Task CataclysmLeagueSlinkBoots_ManualParentMinimumSuspendsAndReactivatesSelectedChildren()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "explicit.stun-recovery", "#% increased Stun and Block Recovery", "explicit")));
        fixture.OpenText(CataclysmLeagueSlinkBootsText);
        var row = Assert.Single(fixture.Window.CurrentSearchState!.Modifiers, modifier =>
            modifier.Text.Contains("Stun and Block Recovery", StringComparison.Ordinal));
        Assert.Equal("31", row.MinimumText);

        fixture.Window.RaiseModifierBoundsChanged(row.SourceIndex, "20", string.Empty);
        Assert.Equal("20", CurrentRow().MinimumText);

        fixture.Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(row.SourceIndex, 0, isSelected: true);
        Assert.Equal("9", CurrentRow().MinimumText);
        fixture.Window.RaiseModifierContributorSelectionChanged(row.SourceIndex, 1, isSelected: true);
        Assert.Equal("31", CurrentRow().MinimumText);

        fixture.Window.RaiseModifierContributorSelectionChanged(row.SourceIndex, 0, isSelected: false);
        Assert.Equal("22", CurrentRow().MinimumText);
        fixture.Window.RaiseModifierContributorSelectionChanged(row.SourceIndex, 1, isSelected: false);
        Assert.Equal("31", CurrentRow().MinimumText);

        fixture.Window.RaiseModifierContributorSelectionChanged(row.SourceIndex, 0, isSelected: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(row.SourceIndex, 1, isSelected: true);
        Assert.Equal("31", CurrentRow().MinimumText);

        fixture.Window.RaiseModifierBoundsChanged(row.SourceIndex, "20", string.Empty);
        var suspended = CurrentRow();
        Assert.Equal("20", suspended.MinimumText);
        Assert.All(suspended.Contributors, contributor =>
        {
            Assert.True(contributor.IsSelected);
            Assert.True(contributor.IsInactive);
            Assert.Equal(
                SearchComponentContributorInactiveReason.ParentBoundBelowSelectedChildFloor,
                contributor.InactiveReason);
        });

        await fixture.SearchAsync();
        using var suspendedDocument = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
            Assert.Single(fixture.SearchClient.Calls).Request!));
        var suspendedFilter = Assert.Single(suspendedDocument.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("explicit.stun-recovery", suspendedFilter.GetProperty("id").GetString());
        Assert.Equal(20, suspendedFilter.GetProperty("value").GetProperty("min").GetInt32());

        fixture.Window.RaiseModifierBoundsChanged(row.SourceIndex, "31", string.Empty);
        var reactivated = CurrentRow();
        Assert.All(reactivated.Contributors, contributor =>
        {
            Assert.True(contributor.IsSelected);
            Assert.False(contributor.IsInactive);
        });

        await fixture.SearchAsync();
        using var restoredDocument = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
            fixture.SearchClient.Calls[1].Request!));
        var restoredFilter = Assert.Single(restoredDocument.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("explicit.stun-recovery", restoredFilter.GetProperty("id").GetString());
        Assert.Equal(31, restoredFilter.GetProperty("value").GetProperty("min").GetInt32());

        PriceCheckerModifierViewModel CurrentRow() => Assert.Single(
            fixture.Window.CurrentSearchState!.Modifiers,
            modifier => modifier.SourceIndex == row.SourceIndex);
    }

    [Fact]
    public async Task StygianViseUnselectedAbyssalSocketRemainsCategoryBeltSearch()
    {
        var fixture = Fixture.Create(Catalog());
        var draft = fixture.OpenText(CorpusItem(10));

        await fixture.SearchAsync();

        Assert.Equal(BaseSearchMode.Category, draft.Base.ActiveCriterion?.Mode);
        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("type", out _));
        Assert.Equal("accessory.belt", Category(query));
        Assert.Empty(StatIds(query));
        var state = Assert.IsType<PriceCheckerWindowState>(fixture.Window.CurrentState);
        Assert.Equal(BaseSearchMode.Category, state.Draft.Base.ActiveCriterion?.Mode);
    }

    [Fact]
    public void ParsedOrdinaryImplicitsCarryBaseImplicitProvenanceWithoutReminderOrUnscalableText()
    {
        var fixture = Fixture.Create(OrganicCatalog());
        var draft = fixture.OpenText(CorpusItem(7));

        var physical = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("additional Physical Damage Reduction", StringComparison.Ordinal));
        var cannotRoll = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Cannot roll Modifiers of Non-Physical Damage Types", StringComparison.Ordinal));

        Assert.Equal(ParsedModifierKind.Implicit, physical.ParsedKind);
        Assert.Equal(ParsedModifierKind.Implicit, cannotRoll.ParsedKind);
        Assert.Equal("UulNetolBreachRingImplicit", physical.ResolvedModifierId);
        Assert.Equal("UulNetolBreachRingImplicit", cannotRoll.ResolvedModifierId);
        Assert.Equal(["base_additional_physical_damage_reduction_%"], physical.ResolvedStatIds);
        Assert.Equal(["breach_ring_uulnetol_implicit"], cannotRoll.ResolvedStatIds);
        Assert.DoesNotContain("Unscalable Value", cannotRoll.OriginalText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingImplicitProviderCandidateRemainsExplicitlyUnresolved()
    {
        var fixture = Fixture.Create(Catalog());
        fixture.OpenText(CorpusItem(4));
        var unsupported = fixture.SelectRow("Cannot roll Caster Modifiers");

        Assert.False(unsupported.IsInteractionEnabled);
        Assert.False(Assert.Single(fixture.Window.CurrentSearchState!.Modifiers, row =>
            row.SourceIndex == unsupported.SourceIndex).IsSelected);
        Assert.True(fixture.Window.CurrentSearchState!.CanSearch);
        Assert.Empty(fixture.SearchClient.Calls);
    }

    [Fact]
    public async Task GaleWrapDualEldritchImplicitsResolveBoundsAndSerializeExactOfficialStats()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "implicit.stat_1871056256", "#% of Physical Damage from Hits taken as Cold Damage", "implicit"),
            Stat(1, "implicit.stat_3714003708", "+#% to Critical Strike Multiplier for Attack Damage", "implicit")));
        var draft = fixture.OpenText(CorpusItem(11));

        var eater = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Physical Damage from Hits taken as Cold Damage", StringComparison.Ordinal));
        var exarch = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Critical Strike Multiplier for Attack Damage", StringComparison.Ordinal));
        Assert.Equal("PhysicalDamageTakenAsColdBodyUberEldritchImplicit1", eater.ResolvedModifierId);
        Assert.Equal("AttackCriticalStrikeMultiplierEldritchImplicit2", exarch.ResolvedModifierId);
        Assert.Equal(ParsedModifierKind.Implicit, eater.ParsedKind);
        Assert.Equal(ParsedModifierKind.Implicit, exarch.ParsedKind);
        Assert.Equal(ParsedImplicitModifierOrigin.EaterOfWorlds, eater.ImplicitOrigin);
        Assert.Equal(ParsedImplicitModifierOrigin.SearingExarch, exarch.ImplicitOrigin);
        Assert.Equal(ModifierLocality.Global, eater.Locality);
        Assert.Equal(ModifierLocality.Global, exarch.Locality);
        Assert.Single(eater.ResolvedStatIds);
        Assert.Single(exarch.ResolvedStatIds);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, eater.ProviderResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, exarch.ProviderResolutionStatus);
        Assert.Equal("implicit.stat_1871056256", eater.ProviderStatId);
        Assert.Equal("implicit.stat_3714003708", exarch.ProviderStatId);
        Assert.True(eater.SupportsValueBounds);
        Assert.True(exarch.SupportsValueBounds);
        Assert.Equal(10m, eater.RequestedMinimum);
        Assert.Equal(23m, exarch.RequestedMinimum);
        Assert.Empty(fixture.SearchClient.Calls);

        fixture.SelectRow("Physical Damage from Hits taken as Cold Damage");
        fixture.SelectRow("Critical Strike Multiplier for Attack Damage");
        await fixture.SearchAsync();

        using var document = JsonDocument.Parse(fixture.SingleSearchJson());
        var query = document.RootElement.GetProperty("query");
        var filters = query.GetProperty("stats")[0].GetProperty("filters").EnumerateArray().ToArray();
        Assert.Equal(
            ["implicit.stat_1871056256", "implicit.stat_3714003708"],
            filters.Select(filter => filter.GetProperty("id").GetString()));
        Assert.Equal(10m, filters[0].GetProperty("value").GetProperty("min").GetDecimal());
        Assert.Equal(23m, filters[1].GetProperty("value").GetProperty("min").GetDecimal());
        Assert.DoesNotContain("Searing Exarch Item", fixture.SingleSearchJson(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Eater of Worlds Item", fixture.SingleSearchJson(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnresolvedEldritchImplicitDoesNotBlockSelectedResolvedImplicit()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "implicit.stat_1871056256", "#% of Physical Damage from Hits taken as Cold Damage", "implicit"),
            Stat(1, "implicit.stat_3714003708", "+#% to Critical Strike Multiplier for Attack Damage", "implicit")));
        var draft = fixture.OpenText(CorpusItem(11).Replace(
            "10% of Physical Damage from Hits taken as Cold Damage",
            "999% of Physical Damage from Hits taken as Cold Damage",
            StringComparison.Ordinal));

        var unresolved = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("999% of Physical Damage", StringComparison.Ordinal));
        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, unresolved.ResolutionStatus);
        fixture.SelectRow("Critical Strike Multiplier for Attack Damage");

        await fixture.SearchAsync();

        using var document = JsonDocument.Parse(fixture.SingleSearchJson());
        Assert.Equal(["implicit.stat_3714003708"], StatIds(document.RootElement.GetProperty("query")));
    }

    [Fact]
    public async Task SynthesisedHelmetResolvedImplicitAppearsFirstAndSerializesWithoutStateFilter()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "implicit.stat_4052037485", "+# to maximum Energy Shield (Local)", "implicit"),
            Stat(1, "explicit.stat_3299347043", "+# to maximum Life", "explicit")));
        var draft = fixture.OpenText(SynthesisedHelmetText);

        Assert.Contains("Synthesised Item", draft.ItemStates);
        var synthesisImplicit = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("maximum Energy Shield", StringComparison.Ordinal));
        Assert.Equal("SynthesisImplicitFlatEnergyShield5_", synthesisImplicit.ResolvedModifierId);
        Assert.Equal(ParsedModifierKind.Implicit, synthesisImplicit.ParsedKind);
        Assert.Equal(ModifierLocality.Local, synthesisImplicit.Locality);
        Assert.True(synthesisImplicit.SupportsValueBounds);
        Assert.Equal(24m, synthesisImplicit.RequestedMinimum);
        Assert.Equal(
            synthesisImplicit.SourceModifierIndex,
            fixture.Window.CurrentSearchState!.Modifiers[0].SourceIndex);
        Assert.Empty(fixture.SearchClient.Calls);

        fixture.SelectRow("maximum Energy Shield");
        await fixture.SearchAsync();

        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal(["implicit.stat_4052037485"], StatIds(query));
        Assert.DoesNotContain("Synthesised Item", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnresolvedSynthesisImplicitBlocksOnlyWhenSelectedAndExactBaseOrdinarySearchStillWorks()
    {
        var fixture = Fixture.Create(Catalog(
            Stat(0, "implicit.stat_4052037485", "+# to maximum Energy Shield (Local)", "implicit"),
            Stat(1, "explicit.stat_3299347043", "+# to maximum Life", "explicit")));
        var draft = fixture.OpenText(SynthesisedHelmetText.Replace(
            "+24(22-25) to maximum Energy Shield",
            "+999(999-999) to maximum Energy Shield",
            StringComparison.Ordinal));
        var unresolved = Assert.Single(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("999(999-999)", StringComparison.Ordinal));
        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, unresolved.ResolutionStatus);

        var unresolvedRow = fixture.SelectRow("999(999-999)");
        Assert.False(unresolvedRow.IsInteractionEnabled);
        Assert.False(Assert.Single(fixture.Window.CurrentSearchState!.Modifiers, row =>
            row.SourceIndex == unresolvedRow.SourceIndex).IsSelected);
        Assert.True(fixture.Window.CurrentSearchState!.CanSearch);
        Assert.Empty(fixture.SearchClient.Calls);

        fixture.Window.RaiseModifierSelectionChanged(unresolvedRow.SourceIndex, isSelected: false);
        fixture.SelectRow("maximum Life");
        fixture.Window.RaiseBaseCriterionToggleRequested();
        Assert.Empty(fixture.SearchClient.Calls);

        await fixture.SearchAsync();

        var json = fixture.SingleSearchJson();
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("Reaver Helmet", query.GetProperty("type").GetString());
        Assert.Equal(["explicit.stat_3299347043"], StatIds(query));
        Assert.DoesNotContain("Synthesised Item", json, StringComparison.OrdinalIgnoreCase);
    }

    private static PathOfExileTradeStatCatalog OrganicCatalog()
    {
        return Catalog(
            Stat(0, "implicit.phys_reduction", "#% additional Physical Damage Reduction", "implicit"),
            Stat(1, "implicit.non_phys", "Cannot roll Modifiers of Non-Physical Damage Types", "implicit"));
    }

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] stats)
    {
        return new PathOfExileTradeStatCatalog(stats);
    }

    private static PathOfExileTradeStatEntry Stat(
        int order,
        string id,
        string text,
        string group)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = group,
            GroupLabel = group,
            Id = id,
            Text = text,
            Type = group,
        };
    }

    private static string Rarity(JsonElement query)
    {
        return query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("rarity")
            .GetProperty("option")
            .GetString()!;
    }

    private static string Category(JsonElement query)
    {
        return query
            .GetProperty("filters")
            .GetProperty("type_filters")
            .GetProperty("filters")
            .GetProperty("category")
            .GetProperty("option")
            .GetString()!;
    }

    private static string[] StatIds(JsonElement query)
    {
        return query
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .Select(filter =>
            {
                Assert.False(filter.TryGetProperty("min", out _));
                Assert.False(filter.TryGetProperty("max", out _));
                Assert.All(filter.EnumerateObject(), property =>
                    Assert.Contains(property.Name, new[] { "id", "value" }));
                if (filter.TryGetProperty("value", out var value))
                {
                    Assert.All(value.EnumerateObject(), property =>
                        Assert.Contains(property.Name, new[] { "min", "max" }));
                }

                return filter.GetProperty("id").GetString()!;
            })
            .ToArray();
    }

    private static string CorpusItem(int index)
    {
        var corpusPath = FindRepoFile("PoEnhance.Core.Tests", "TestData", "Items", "advanced-real-items-corpus.txt");
        var corpus = File.ReadAllText(corpusPath);
        var items = new Regex(@"\r?\n\s*\r?\n(?=Item Class:)", RegexOptions.CultureInvariant)
            .Split(corpus.TrimEnd('\r', '\n'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        return items[index];
    }

    private const string SupremeSpikedShieldText = """
Item Class: Shields
Rarity: Magic
Wasp's Supreme Spiked Shield of Thick Skin
--------
Chance to Block: 24%
Evasion Rating: 362 (augmented)
Energy Shield: 73 (augmented)
--------
Requirements:
Level: 70
Dex: 85
Int: 85
--------
Sockets: B-B-G
--------
Item Level: 84
--------
{ Implicit Modifier }
+5% chance to Suppress Spell Damage
(40% of Damage from Suppressed Hits and Ailments they inflict is prevented)
--------
{ Prefix Modifier "Wasp's" (Tier: 3) - Defences, Evasion, Energy Shield }
31(27-32)% increased Evasion and Energy Shield
13(12-13)% increased Stun and Block Recovery
{ Suffix Modifier "of Thick Skin" (Tier: 6) }
11(11-13)% increased Stun and Block Recovery
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

    private sealed class Fixture
    {
        private Fixture(
            PathOfExileTradeStatCatalog statCatalog,
            FakeWindow window,
            FakeSearchClient searchClient,
            PriceCheckerSearchController controller)
        {
            StatCatalog = statCatalog;
            Window = window;
            SearchClient = searchClient;
            Controller = controller;
        }

        private PathOfExileTradeStatCatalog StatCatalog { get; }

        public FakeWindow Window { get; }

        public FakeSearchClient SearchClient { get; }

        private PriceCheckerSearchController Controller { get; }

        public static Fixture Create(PathOfExileTradeStatCatalog statCatalog)
        {
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
                new FakeFilterCatalogProvider(FilterCatalog()));
            var window = new FakeWindow();
            var controller = new PriceCheckerSearchController(
                service,
                global::PoEnhance.App.Infrastructure.Settings.ApplicationLeagueSetting.CreateTransient("Mirage"));
            controller.AttachWindow(window);
            return new Fixture(statCatalog, window, searchClient, controller);
        }

        public TradeSearchDraft OpenText(string itemText)
        {
            var catalog = LoadGameDataCatalog();
            var parsed = new ItemTextParser().Parse(itemText);
            var displayService = new ParsedItemGameDataDisplayService();
            var baseResolution = displayService.ResolveItemBase(parsed, catalog).Result;
            Assert.NotNull(baseResolution);
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
            var draft = Controller
                .PrepareDraftAsync(draftResult.Draft!)
                .GetAwaiter()
                .GetResult();
            Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));
            return draft;
        }

        public PriceCheckerModifierViewModel SelectRow(string textFragment)
        {
            var row = Assert.Single(Window.CurrentSearchState!.Modifiers, row =>
                row.Text.Contains(textFragment, StringComparison.Ordinal));
            Window.RaiseModifierSelectionChanged(row.SourceIndex, isSelected: true);
            return row;
        }

        public Task SearchAsync()
        {
            return Controller.SearchAsync();
        }

        public string SingleSearchJson()
        {
            Assert.True(
                Window.CurrentSearchState!.Status == PriceCheckerSearchViewStatus.ZeroResults,
                string.Join(
                    Environment.NewLine,
                    [
                        Window.CurrentSearchState.Message,
                        .. Controller.CurrentDeveloperDiagnostics.Diagnostics.Select(diagnostic =>
                            $"{diagnostic.Code}: {diagnostic.Message}"),
                    ]));
            var call = Assert.Single(SearchClient.Calls);
            return PathOfExileTradeJson.SerializeSearchRequest(call.Request!);
        }

        private static PathOfExileTradeFilterCatalog FilterCatalog()
        {
            return new PathOfExileTradeFilterCatalog(
            [
                Category(0, "weapon.wand", "Wand"),
                Category(1, "armour.shield", "Shield"),
                Category(2, "accessory.ring", "Ring"),
                Category(3, "accessory.belt", "Belt"),
                Category(4, "armour.boots", "Boots"),
                Category(5, "armour.helmet", "Helmet"),
                Category(6, "armour.chest", "Body Armour"),
            ], optionFilterDefinitions:
                PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog().OptionFilterDefinitions);
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
    }

    private sealed record SearchCall(PathOfExileTradeSearchRequest? Request);

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
            Calls.Add(new SearchCall(request));
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

        public PriceCheckerPlacement? GetDisplayedPlacement() => null;

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

        public void RaiseModifierContributorSelectionChanged(
            int modifierIndex,
            int contributorIndex,
            bool isSelected)
        {
            ModifierSelectionChanged?.Invoke(
                this,
                new PriceCheckerModifierSelectionChangedEventArgs(
                    modifierIndex,
                    isSelected,
                    contributorIndex));
        }

        public void RaiseModifierContributorBoundsChanged(
            int modifierIndex,
            int contributorIndex,
            string minimumText,
            string maximumText)
        {
            ModifierBoundsChanged?.Invoke(
                this,
                new PriceCheckerModifierBoundsChangedEventArgs(
                    modifierIndex,
                    minimumText,
                    maximumText,
                    contributorIndex));
        }

        public void RaiseModifierBoundsChanged(
            int modifierIndex,
            string minimumText,
            string maximumText)
        {
            ModifierBoundsChanged?.Invoke(
                this,
                new PriceCheckerModifierBoundsChangedEventArgs(
                    modifierIndex,
                    minimumText,
                    maximumText));
        }

        public void RaiseBaseCriterionToggleRequested()
        {
            BaseCriterionToggleRequested?.Invoke(this, EventArgs.Empty);
        }
    }
#pragma warning restore CS0067

    private const string CataclysmLeagueSlinkBootsText = """
Item Class: Boots
Rarity: Rare
Cataclysm League
Slink Boots
--------
Quality: +10% (augmented)
Evasion Rating: 326 (augmented)
--------
Requirements:
Level: 69
Dex: 120
--------
Sockets: G-G-R-G
--------
Item Level: 84
--------
{ Prefix Modifier "Moth's" (Tier: 5) - Defences, Evasion }
14(14-20)% increased Evasion Rating
9(8-9)% increased Stun and Block Recovery
{ Suffix Modifier "of the Troll" (Tier: 3) - Life }
Regenerate 46.8(32.1-48) Life per second
{ Suffix Modifier "of the Whelpling" (Tier: 8) - Elemental, Fire, Resistance }
+6(6-11)% to Fire Resistance
{ Suffix Modifier "of Steel Skin" (Tier: 3) }
22(20-22)% increased Stun and Block Recovery
""";

    private const string SynthesisedHelmetText = """
Item Class: Helmets
Rarity: Rare
Synthesised Item
Gale Dome
Synthesised Reaver Helmet
--------
Energy Shield: 98 (augmented)
--------
Requirements:
Level: 62
Int: 114
--------
Sockets: B-B-B-B
--------
Item Level: 84
--------
{ Implicit Modifier }
+24(22-25) to maximum Energy Shield
--------
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50(50-59) to maximum Life
""";
}

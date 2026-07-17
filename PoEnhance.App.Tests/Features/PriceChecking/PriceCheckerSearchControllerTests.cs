using System.Collections.Immutable;
using System.Globalization;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerSearchControllerTests
{
    [Fact]
    public void UpdateCurrentDraft_PreparesSearchStateWithoutCallingService()
    {
        var fixture = SearchFixture.Create();

        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Controller.CurrentViewState.Status);
        Assert.True(fixture.Controller.CurrentViewState.CanSearch);
        Assert.Empty(fixture.PriceCheckService.Calls);
    }

    [Fact]
    public void ItemPropertyProjection_UsesCanonicalOrderFlattensAggregatesAndHidesGroupedModifiers()
    {
        var fixture = SearchFixture.Create();
        var aggregate = ContributorModifier();
        var flatPhysical = Modifier("Adds 23 to 46 Physical Damage", supportsValueBounds: true, minimum: 34.5m);
        var fire = Modifier("Adds 80 to 129 Fire Damage", supportsValueBounds: true, minimum: 104.5m);
        var dexterity = Modifier("+53 to Dexterity", supportsValueBounds: true, minimum: 53m);
        var draft = Draft("Horror Mangler", modifiers: [aggregate, flatPhysical, fire, dexterity]) with
        {
            ItemProperties = AllItemProperties(),
            ItemPropertyContributionGroups =
            [
                ContributionGroup(TradeSearchItemPropertyKind.PhysicalDps, 0, 1),
                ContributionGroup(TradeSearchItemPropertyKind.ElementalDps, 2),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));

        Assert.Equal(
            Enum.GetValues<TradeSearchItemPropertyKind>(),
            fixture.Window.CurrentSearchState!.ItemProperties.Select(property => property.Kind));
        var standalone = Assert.Single(fixture.Window.CurrentSearchState.Modifiers);
        Assert.Equal(3, standalone.SourceIndex);
        Assert.Contains("Dexterity", standalone.Text, StringComparison.Ordinal);
        Assert.False(standalone.ShowsExpansionControl);
        var physical = fixture.Window.CurrentSearchState.ItemProperties[1];
        Assert.True(physical.HasChildren);
        Assert.False(physical.IsExpanded);
        Assert.Equal([0, 1], physical.Children.Select(child => child.SourceIndex));
        Assert.False(fixture.Window.CurrentSearchState.ItemProperties[0].HasChildren);
        Assert.Equal(7, fixture.Window.CurrentSearchState.Stats.Count);
        Assert.All(
            fixture.Window.CurrentSearchState.Stats.Take(6),
            row => Assert.IsType<PriceCheckerItemPropertyViewModel>(row));
        Assert.Same(standalone, fixture.Window.CurrentSearchState.Stats[6]);
        Assert.Equal(10, fixture.Window.CurrentSearchState.StatsCount);
        Assert.Equal(0, fixture.Window.CurrentSearchState.SelectedStatsCount);
        Assert.Equal(
            [0, 1, 2, 3],
            fixture.Window.CurrentSearchState.ItemProperties
                .SelectMany(property => property.Children)
                .Concat(fixture.Window.CurrentSearchState.Modifiers)
                .Select(modifier => modifier.SourceIndex)
                .Order());

        fixture.Window.RaiseItemPropertyExpansionChanged(physical.SourceIndex, isExpanded: true);

        physical = fixture.Window.CurrentSearchState.ItemProperties[1];
        Assert.True(physical.IsExpanded);
        Assert.Equal([0, 1], physical.Children.Select(child => child.SourceIndex));
        var flattenedAggregate = physical.Children[0];
        Assert.False(flattenedAggregate.ShowsExpansionControl);
        Assert.True(flattenedAggregate.ContributorsVisible);
        Assert.Equal(2, flattenedAggregate.Contributors.Count);
        Assert.Equal(4, draft.ModifierFilters.Count);
        Assert.Equal(4, fixture.Window.CurrentState!.Draft.ModifierFilters.Count);

        fixture.Window.RaiseModifierSelectionChanged(flattenedAggregate.SourceIndex, isSelected: true);
        Assert.False(fixture.Window.CurrentState.Draft.ItemProperties[1].IsSelected);
        Assert.True(fixture.Window.CurrentState.Draft.ModifierFilters[0].IsSelected);
        Assert.Equal("1 child selected", fixture.Window.CurrentSearchState.ItemProperties[1].SelectedChildSummary);
        Assert.True(fixture.Window.CurrentSearchState.ItemProperties[1].HasSelectedChildren);
        Assert.Equal(1, fixture.Window.CurrentSearchState.SelectedStatsCount);

        fixture.Window.RaiseItemPropertyExpansionChanged(2, isExpanded: true);
        Assert.True(fixture.Window.CurrentSearchState.ItemProperties[1].IsExpanded);
        Assert.True(fixture.Window.CurrentSearchState.ItemProperties[2].IsExpanded);
        Assert.True(fixture.Window.CurrentState.Draft.ModifierFilters[0].IsSelected);
        Assert.False(fixture.Window.CurrentState.Draft.ItemProperties[2].IsSelected);

        fixture.Window.RaiseItemPropertyExpansionChanged(1, isExpanded: false);
        Assert.False(fixture.Window.CurrentSearchState.ItemProperties[1].IsExpanded);
        Assert.True(fixture.Window.CurrentSearchState.ItemProperties[2].IsExpanded);
        Assert.True(fixture.Window.CurrentState.Draft.ModifierFilters[0].IsSelected);
    }

    [Fact]
    public void ArmageddonThirst_PropertyGroupsExpandAndCollapseIndependentlyAndNewItemStartsCollapsed()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft(
            "Armageddon Thirst",
            modifiers:
            [
                Modifier("146% increased Physical Damage"),
                Modifier("Adds 80 to 129 Fire Damage"),
                Modifier("20% increased Attack Speed"),
            ]) with
        {
            ItemProperties =
            [
                ItemProperty(TradeSearchItemPropertyKind.PhysicalDps, 169.065m),
                ItemProperty(TradeSearchItemPropertyKind.ElementalDps, 104.5m),
                ItemProperty(TradeSearchItemPropertyKind.AttacksPerSecond, 1.2m),
            ],
            ItemPropertyContributionGroups =
            [
                ContributionGroup(TradeSearchItemPropertyKind.PhysicalDps, 0),
                ContributionGroup(TradeSearchItemPropertyKind.ElementalDps, 1),
                ContributionGroup(TradeSearchItemPropertyKind.AttacksPerSecond, 2),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));

        foreach (var property in fixture.Window.CurrentSearchState!.ItemProperties)
        {
            fixture.Window.RaiseItemPropertyExpansionChanged(property.SourceIndex, isExpanded: true);
        }

        Assert.All(fixture.Window.CurrentSearchState.ItemProperties, property => Assert.True(property.IsExpanded));
        Assert.All(fixture.Window.CurrentSearchState.ItemProperties, property => Assert.Single(property.Children));
        Assert.DoesNotContain(fixture.Window.CurrentSearchState.Modifiers, modifier =>
            draft.ModifierFilters.Any(canonical => canonical.OriginalText == modifier.Text));

        fixture.Window.RaiseItemPropertyExpansionChanged(1, isExpanded: false);
        Assert.True(fixture.Window.CurrentSearchState.ItemProperties[0].IsExpanded);
        Assert.False(fixture.Window.CurrentSearchState.ItemProperties[1].IsExpanded);
        Assert.True(fixture.Window.CurrentSearchState.ItemProperties[2].IsExpanded);
        Assert.DoesNotContain(fixture.Window.CurrentState!.Draft.ItemProperties, property => property.IsSelected);
        Assert.DoesNotContain(fixture.Window.CurrentState.Draft.ModifierFilters, modifier => modifier.IsSelected);
        Assert.Empty(fixture.PriceCheckService.Calls);

        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));
        Assert.All(fixture.Window.CurrentSearchState.ItemProperties, property => Assert.False(property.IsExpanded));
    }

    [Fact]
    public async Task ItemPropertyEditing_UpdatesCanonicalDraftInvalidatesResultsAndBlocksInvalidRange()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("id-1")], total: 1);
        var draft = Draft("Horror Mangler") with
        {
            ItemProperties =
            [
                ItemProperty(TradeSearchItemPropertyKind.PhysicalDps, 169.065m),
                ItemProperty(TradeSearchItemPropertyKind.AttacksPerSecond, 1.2m),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));
        fixture.Window.RaiseItemPropertySelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();
        Assert.True(fixture.Window.CurrentSearchState!.CanOpenTrade);
        Assert.Single(fixture.PriceCheckService.Calls);

        fixture.Window.RaiseItemPropertyBoundsChanged(0, "169.065", "250.125");

        var property = fixture.Window.CurrentState!.Draft.ItemProperties[0];
        Assert.Equal(169.065m, property.RequestedMinimum);
        Assert.Equal(250.125m, property.RequestedMaximum);
        Assert.True(property.IsSelected);
        Assert.Empty(fixture.Window.CurrentSearchState!.Offers);
        Assert.False(fixture.Window.CurrentSearchState.CanOpenTrade);
        Assert.Single(fixture.PriceCheckService.Calls);

        fixture.Window.RaiseItemPropertySelectionChanged(1, isSelected: true);
        fixture.Window.RaiseItemPropertyBoundsChanged(1, "1.20", string.Empty);
        var aps = fixture.Window.CurrentState.Draft.ItemProperties[1].RequestedMinimum;
        Assert.Equal(1.20m, aps);
        Assert.Equal(2, (decimal.GetBits(aps)[3] >> 16) & 0x7F);

        fixture.Window.RaiseItemPropertyBoundsChanged(0, "300", "200");
        await fixture.Controller.SearchAsync();

        Assert.False(fixture.Window.CurrentSearchState.CanSearch);
        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState.Status);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Contains(
            fixture.Window.CurrentState.ValidationResult.Diagnostics,
            diagnostic => diagnostic.Code == TradeSearchValidationDiagnosticCodes.InvalidItemPropertyRange);
    }

    [Fact]
    public void ItemPropertyProviderStates_EnableFiveKindsAndKeepChaosVisibleDisabled()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Chaos Edge") with { ItemProperties = AllItemProperties() };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));

        Assert.Equal(6, fixture.Window.CurrentSearchState!.ItemProperties.Count);
        Assert.Equal(5, fixture.Window.CurrentSearchState.ItemProperties.Count(property => property.IsAvailable));
        var chaos = Assert.Single(fixture.Window.CurrentSearchState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ChaosDps);
        Assert.False(chaos.IsAvailable);
        Assert.False(chaos.CanEditBounds);
        Assert.Equal(
            "Path of Exile Trade does not expose a Chaos DPS filter.",
            chaos.AvailabilityReason);

        fixture.Window.RaiseItemPropertySelectionChanged(chaos.SourceIndex, isSelected: true);
        Assert.False(fixture.Window.CurrentState!.Draft.ItemProperties[chaos.SourceIndex].IsSelected);
    }

    [Fact]
    public async Task UnresolvedProperty_RemainsVisibleBlocksOnlyWhileSelectedAndDoesNotBlockModifierSearch()
    {
        var fixture = SearchFixture.Create();
        var reason = "The Trade filter catalog could not be loaded.";
        var unresolved = ItemProperty(TradeSearchItemPropertyKind.PhysicalDps, 169.065m) with
        {
            IsSelected = true,
            IsSearchable = false,
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            NotSearchableReason = reason,
        };
        var draft = Draft(
            "Catalog Failure Axe",
            modifiers: [Modifier("20% increased Attack Speed", isSelected: true)]) with
        {
            ItemProperties = [unresolved],
        };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));

        var row = Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties);
        Assert.True(row.IsSelected);
        Assert.False(row.IsAvailable);
        Assert.Equal(reason, row.AvailabilityReason);
        Assert.False(fixture.Window.CurrentSearchState.CanSearch);

        fixture.Window.RaiseItemPropertySelectionChanged(row.SourceIndex, isSelected: false);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.False(call.Draft!.ItemProperties[0].IsSelected);
        Assert.True(call.Draft.ModifierFilters[0].IsSelected);
    }

    [Fact]
    public void ResetItem_RestoresPropertyAndGroupedModifierStateWithoutRequests()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Horror Mangler", modifiers: [ContributorModifier()]) with
        {
            ItemProperties = [ItemProperty(TradeSearchItemPropertyKind.PhysicalDps, 169.065m)],
            ItemPropertyContributionGroups =
                [ContributionGroup(TradeSearchItemPropertyKind.PhysicalDps, 0)],
        };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));
        fixture.Window.RaiseItemPropertyExpansionChanged(0, isExpanded: true);
        fixture.Window.RaiseItemPropertySelectionChanged(0, isSelected: true);
        fixture.Window.RaiseItemPropertyBoundsChanged(0, "180.125", "250.5");
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 1, isSelected: true);

        fixture.Window.RaiseResetItemRequested();

        var property = Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties);
        Assert.False(property.IsSelected);
        Assert.Equal("169.065", property.MinimumText);
        Assert.Empty(property.MaximumText);
        Assert.True(property.IsExpanded);
        var child = Assert.Single(property.Children);
        Assert.False(child.IsSelected);
        Assert.All(child.Contributors, contributor => Assert.False(contributor.IsSelected));
        Assert.Single(fixture.Window.CurrentState!.Draft.ItemPropertyContributionGroups);
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task ItemPropertyAndChildSelections_ReachSearchIndependentlyThroughController()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Horror Mangler", modifiers: [ContributorModifier()]) with
        {
            ItemProperties = [ItemProperty(TradeSearchItemPropertyKind.PhysicalDps, 169.065m)],
            ItemPropertyContributionGroups =
                [ContributionGroup(TradeSearchItemPropertyKind.PhysicalDps, 0)],
        };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));

        fixture.Window.RaiseItemPropertySelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();

        var propertyOnly = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.True(propertyOnly.Draft!.ItemProperties[0].IsSelected);
        Assert.False(propertyOnly.Draft.ModifierFilters[0].IsSelected);

        fixture.Window.RaiseItemPropertyExpansionChanged(0, isExpanded: true);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();

        Assert.Equal(2, fixture.PriceCheckService.Calls.Count);
        var combined = fixture.PriceCheckService.Calls[1].Draft!;
        Assert.True(combined.ItemProperties[0].IsSelected);
        Assert.True(combined.ModifierFilters[0].IsSelected);
    }

    [Fact]
    public async Task ItemPropertyAndChild_KeepIndependentSelectionBoundsSearchAndCollapsedIndication()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft(
            "Armageddon Thirst",
            modifiers: [Modifier("20% increased Attack Speed", supportsValueBounds: true, minimum: 20m)]) with
        {
            ItemProperties = [ItemProperty(TradeSearchItemPropertyKind.AttacksPerSecond, 1.2m)],
            ItemPropertyContributionGroups =
                [ContributionGroup(TradeSearchItemPropertyKind.AttacksPerSecond, 0)],
        };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));
        var aps = Assert.Single(fixture.Window.CurrentSearchState!.ItemProperties);
        Assert.False(aps.IsExpanded);
        Assert.False(aps.HasSelectedChildren);

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        aps = Assert.Single(fixture.Window.CurrentSearchState.ItemProperties);
        Assert.False(aps.IsSelected);
        Assert.False(aps.IsExpanded);
        Assert.Equal("1 child selected", aps.SelectedChildSummary);
        Assert.True(aps.HasSelectedChildren);
        Assert.Empty(fixture.PriceCheckService.Calls);
        await fixture.Controller.SearchAsync();
        var childOnly = Assert.Single(fixture.PriceCheckService.Calls).Draft!;
        Assert.False(childOnly.ItemProperties[0].IsSelected);
        Assert.True(childOnly.ModifierFilters[0].IsSelected);

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);
        fixture.Window.RaiseItemPropertySelectionChanged(0, isSelected: true);
        Assert.Single(fixture.PriceCheckService.Calls);
        await fixture.Controller.SearchAsync();
        var parentOnly = fixture.PriceCheckService.Calls[1].Draft!;
        Assert.True(parentOnly.ItemProperties[0].IsSelected);
        Assert.False(parentOnly.ModifierFilters[0].IsSelected);

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierBoundsChanged(0, "21,5", "25,25");
        fixture.Window.RaiseItemPropertyBoundsChanged(0, "1,35", "2,5");

        Assert.Equal(2, fixture.PriceCheckService.Calls.Count);
        Assert.Equal(1.35m, fixture.Window.CurrentState!.Draft.ItemProperties[0].RequestedMinimum);
        Assert.Equal(2.5m, fixture.Window.CurrentState.Draft.ItemProperties[0].RequestedMaximum);
        Assert.Equal(21.5m, fixture.Window.CurrentState.Draft.ModifierFilters[0].RequestedMinimum);
        Assert.Equal(25.25m, fixture.Window.CurrentState.Draft.ModifierFilters[0].RequestedMaximum);
        Assert.True(fixture.Window.CurrentState.Draft.ItemProperties[0].IsSelected);
        Assert.True(fixture.Window.CurrentState.Draft.ModifierFilters[0].IsSelected);
        Assert.False(Assert.Single(fixture.Window.CurrentSearchState.ItemProperties).IsExpanded);
        Assert.Empty(fixture.Window.CurrentSearchState.Offers);
        Assert.False(fixture.Window.CurrentSearchState.CanOpenTrade);

        await fixture.Controller.SearchAsync();
        var combined = fixture.PriceCheckService.Calls[2].Draft!;
        Assert.True(combined.ItemProperties[0].IsSelected);
        Assert.True(combined.ModifierFilters[0].IsSelected);
    }

    [Fact]
    public async Task UnsupportedChaosParent_DoesNotBlockItsIndependentlySelectedChild()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft(
            "Chaos Edge",
            modifiers: [Modifier("Adds 10 to 20 Chaos Damage", supportsValueBounds: true, minimum: 15m)]) with
        {
            ItemProperties = [ItemProperty(
                TradeSearchItemPropertyKind.ChaosDps,
                19.5m,
                supported: false)],
            ItemPropertyContributionGroups =
                [ContributionGroup(TradeSearchItemPropertyKind.ChaosDps, 0)],
        };
        fixture.Controller.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));
        fixture.Window.RaiseItemPropertyExpansionChanged(0, isExpanded: true);
        fixture.Window.RaiseItemPropertySelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.False(call.Draft!.ItemProperties[0].IsSelected);
        Assert.True(call.Draft.ModifierFilters[0].IsSelected);
    }

    [Fact]
    public void UpdateCurrentDraft_AggregationDiagnosticsStayInDeveloperSurface()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Armoured Shell") with
        {
            ModifierAggregationDiagnostics =
            [
                new TradeSearchDraftDiagnostic(
                    TradeSearchDraftDiagnosticCodes.ModifierAggregationSkipped,
                    "Canonical modifier aggregation was skipped: incompatible numeric shape."),
            ],
        };

        fixture.Controller.UpdateCurrentDraft(draft, ValidationSuccess());

        var diagnostic = Assert.Single(fixture.Controller.CurrentDeveloperDiagnostics.Diagnostics);
        Assert.Equal(TradeSearchDraftDiagnosticCodes.ModifierAggregationSkipped, diagnostic.Code);
        Assert.Contains("incompatible numeric shape", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("aggregation", fixture.Window.CurrentSearchState?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetItem_RestoresInitialEditableStateWithoutRequestsOrPresentationChanges()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [Offer("id-1")],
            total: 2,
            resultIds: ["id-1", "id-2"],
            fetchedResultIds: ["id-1"]);
        var draft = DraftWithBothBaseCriteria() with
        {
            ModifierFilters = [ContributorModifier()],
        };
        var placement = new PriceCheckerPlacement(10, 20, 400, 500);
        fixture.Window.ApplyPlacement(placement);
        fixture.Window.SetPinned(true);
        fixture.Controller.UpdateCurrentDraft(draft, ValidationSuccess());
        fixture.Window.RaiseModifierExpansionChanged(0, isExpanded: true);
        await fixture.Controller.SearchAsync();

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "25", "40");
        fixture.Window.RaiseModifierBoundsChanged(0, "20", "60");
        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-parent-fractured");
        fixture.Window.RaiseBaseCriterionToggleRequested();
        await fixture.Controller.SearchAsync();
        Assert.True(fixture.Window.CurrentSearchState!.CanOpenTrade);

        fixture.Window.RaiseResetItemRequested();

        var row = Assert.Single(fixture.Window.CurrentSearchState!.Modifiers);
        Assert.False(row.IsSelected);
        Assert.Equal("146", row.MinimumText);
        Assert.Empty(row.MaximumText);
        Assert.Equal("Pseudo", row.SelectedFilterVariant?.Label);
        Assert.True(row.IsExpanded);
        Assert.All(row.Contributors, contributor =>
        {
            Assert.False(contributor.IsSelected);
            Assert.False(contributor.IsInactive);
            Assert.Equal("", contributor.MaximumText);
        });
        Assert.Equal(BaseSearchMode.Category, fixture.Window.CurrentState!.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState.Status);
        Assert.Equal("Ready to search.", fixture.Window.CurrentSearchState.Message);
        Assert.Empty(fixture.Window.CurrentSearchState.Offers);
        Assert.False(fixture.Window.CurrentSearchState.CanLoadMore);
        Assert.False(fixture.Window.CurrentSearchState.CanOpenTrade);
        Assert.Same(placement, fixture.Window.CurrentPlacement);
        Assert.True(fixture.Window.IsPinned);
        Assert.Equal(2, fixture.PriceCheckService.Calls.Count);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task ResetItem_CancelsStaleSearchAndNewItemReplacesItsSnapshot()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(Draft("First Loop"), ValidationSuccess());

        var pendingSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        fixture.Window.RaiseResetItemRequested();
        completion.SetResult(SuccessResult([Offer("old")], total: 1));
        await pendingSearch;

        Assert.Empty(fixture.Window.CurrentSearchState!.Offers);
        Assert.False(fixture.Window.CurrentSearchState.CanOpenTrade);
        Assert.Equal("First Loop", fixture.Window.CurrentState!.Draft.DisplayName);

        fixture.Controller.UpdateCurrentDraft(
            Draft("Second Loop", modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseResetItemRequested();

        Assert.Equal("Second Loop", fixture.Window.CurrentState!.Draft.DisplayName);
        Assert.False(Assert.Single(fixture.Window.CurrentSearchState.Modifiers).IsSelected);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public void AttachWindow_CleanProfileDefaultsLeagueToMirage()
    {
        var fixture = SearchFixture.Create();

        Assert.Equal("Mirage", fixture.Window.CurrentSearchState?.LeagueIdentifier);
    }

    [Fact]
    public void SelectedSupportedModifier_EnablesBoundsAndKeepsObservedMinimum()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [Modifier("52% increased Physical Damage", supportsValueBounds: true, minimum: 52m)]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        var row = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.True(row.CanEditBounds);
        Assert.Equal("52", row.MinimumText);
        Assert.Empty(row.MaximumText);
    }

    [Fact]
    public void SelectedUnsupportedModifier_PublishesItsBoundReasonToDeveloperDiagnostics()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier(
                        "Unsupported value",
                        valueBoundsUnsupportedReason: "The translation has multiple numeric provider values."),
                ]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        var diagnostic = Assert.Single(fixture.Controller.CurrentDeveloperDiagnostics.Diagnostics);
        Assert.Equal("MODIFIER_BOUNDS_UNSUPPORTED", diagnostic.Code);
        Assert.Equal("The translation has multiple numeric provider values.", diagnostic.Message);
    }

    [Fact]
    public async Task ModifierBounds_InvalidTextBlocksSearchAndPreservesText()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [Modifier("52% increased Physical Damage", supportsValueBounds: true, minimum: 52m)]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierBoundsChanged(0, "abc", string.Empty);

        await fixture.Controller.SearchAsync();

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal("Modifier Min and Max must be finite decimal numbers.", fixture.Window.CurrentSearchState?.Message);
        Assert.Equal("abc", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);
    }

    [Fact]
    public async Task MixedDomainAggregateWithoutWideProviderVariantBlocksSearchWithoutRequest()
    {
        var fixture = SearchFixture.Create();
        var aggregate = Modifier(
            "146% increased Physical Damage",
            supportsValueBounds: true,
            minimum: 146m) with
        {
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Unsupported,
            ProviderDiagnosticCode =
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable,
            ProviderDiagnosticMessage =
                "No aggregate-wide Trade variant covers contributor domains [Crafted, Explicit].",
            Sources =
            [
                new SearchComponentSourceProvenance
                {
                    ComponentId = "modifier:0:0",
                    ProviderDomain = "Explicit",
                },
                new SearchComponentSourceProvenance
                {
                    ComponentId = "modifier:1:0",
                    ProviderDomain = "Crafted",
                },
            ],
        };
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [aggregate]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Contains(
            fixture.Controller.CurrentDeveloperDiagnostics.Diagnostics,
            diagnostic => diagnostic.Code ==
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable &&
                diagnostic.Message.Contains(
                    "No aggregate-wide Trade variant",
                    StringComparison.Ordinal));
        Assert.Equal(
            "Select a supported Trade search.",
            fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task ModifierBounds_EditingRetainsTheExistingRowsAndDoesNotRequestSearchOrFetch()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [Modifier("Test Value", supportsValueBounds: true)]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        var rows = fixture.Window.CurrentSearchState!.Modifiers;
        var row = Assert.Single(rows);
        var updateCount = fixture.Window.ModifierCollections.Count;

        fixture.Window.RaiseModifierBoundsChanged(0, "1", string.Empty);
        fixture.Window.RaiseModifierBoundsChanged(0, "12", string.Empty);
        fixture.Window.RaiseModifierBoundsChanged(0, "123", string.Empty);
        fixture.Window.RaiseModifierBoundsChanged(0, "-", string.Empty);
        fixture.Window.RaiseModifierBoundsChanged(0, "-15", string.Empty);
        fixture.Window.RaiseModifierBoundsChanged(0, "2.", string.Empty);
        fixture.Window.RaiseModifierBoundsChanged(0, "2,83", string.Empty);
        fixture.Window.RaiseModifierBoundsChanged(0, string.Empty, string.Empty);

        Assert.Same(rows, fixture.Window.CurrentSearchState?.Modifiers);
        Assert.Same(row, Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []));
        Assert.All(fixture.Window.ModifierCollections.Skip(updateCount), collection => Assert.Same(rows, collection));
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
        Assert.Equal(string.Empty, row.MinimumText);

        fixture.Window.RaiseModifierBoundsChanged(0, "-", string.Empty);
        await fixture.Controller.SearchAsync();
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal("Modifier Min and Max must be finite decimal numbers.", fixture.Window.CurrentSearchState?.Message);
        Assert.Equal("-", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);
    }

    [Fact]
    public async Task ModifierBounds_ChangedBoundsInvalidateResultsAndTravelToSearchDraft()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("id-1")], total: 1, resultIds: ["id-1"], fetchedResultIds: ["id-1"]);
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [Modifier("52% increased Physical Damage", supportsValueBounds: true, minimum: 52m)]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();

        fixture.Window.RaiseModifierBoundsChanged(0, "40", "60");

        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        await fixture.Controller.SearchAsync();
        var modifier = Assert.Single(fixture.PriceCheckService.Calls[^1].Draft?.ModifierFilters ?? []);
        Assert.Equal(40m, modifier.RequestedMinimum);
        Assert.Equal(60m, modifier.RequestedMaximum);
    }

    [Fact]
    public async Task ModifierFilterVariant_ChangeInvalidatesSearchWithoutAutomaticSearchOrFetch()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [Offer("id-1")],
            total: 2,
            resultIds: ["id-1", "id-2"],
            fetchedResultIds: ["id-1"]);
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [VariantModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();
        Assert.True(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.True(fixture.Window.CurrentSearchState?.CanOpenTrade);

        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-pseudo");

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);
        var selectedRow = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.True(selectedRow.IsSelected);
        Assert.Equal("Pseudo", selectedRow.SelectedFilterVariant?.Label);
        Assert.Equal("20", selectedRow.MinimumText);
        Assert.Equal(string.Empty, selectedRow.MaximumText);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
        Assert.Equal(
            "variant-pseudo",
            Assert.Single(fixture.Window.CurrentState?.Draft.ModifierFilters ?? []).SelectedFilterVariantIdentity);
    }

    [Fact]
    public void ModifierFilterVariant_DeselectionAndReselectionRetainTheChosenType()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [VariantModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-implicit");
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        var row = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.True(row.IsSelected);
        Assert.Equal("Implicit", row.SelectedFilterVariant?.Label);
        Assert.Equal(
            "variant-implicit",
            Assert.Single(fixture.Window.CurrentState?.Draft.ModifierFilters ?? []).SelectedFilterVariantIdentity);
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task ModifierFilterVariant_UnavailableChosenIdentityBlocksSearchLocally()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.EffectiveDraftResolver = draft => draft with
        {
            ModifierFilters = draft.ModifierFilters.Select(modifier =>
                modifier.SelectedFilterVariantIdentity == "variant-implicit"
                    ? modifier with
                    {
                        FilterVariants = modifier.FilterVariants
                            .Where(option => option.Identity != "variant-implicit")
                            .ToArray(),
                        ProviderResolutionStatus = SearchComponentProviderResolutionStatus.NotFound,
                        ProviderStatId = null,
                    }
                    : modifier).ToArray(),
        };
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [VariantModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-implicit");

        await fixture.Controller.SearchAsync();

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Contains(
            fixture.Controller.CurrentDeveloperDiagnostics.Diagnostics,
            diagnostic => diagnostic.Code ==
                TradeSearchValidationDiagnosticCodes.SelectedModifierVariantUnresolved);
        Assert.Equal(
            "variant-implicit",
            Assert.Single(fixture.Window.CurrentState?.Draft.ModifierFilters ?? []).SelectedFilterVariantIdentity);
    }

    [Theory]
    [InlineData("variant-explicit")]
    [InlineData("variant-implicit")]
    [InlineData("variant-crafted")]
    [InlineData("variant-fractured")]
    [InlineData("variant-pseudo")]
    public async Task ModifierFilterVariant_SearchReceivesExactlyTheChosenOpaqueIdentity(
        string chosenIdentity)
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [VariantModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierFilterVariantChanged(0, chosenIdentity);

        await fixture.Controller.SearchAsync();

        var searched = Assert.Single(Assert.Single(fixture.PriceCheckService.Calls).Draft?.ModifierFilters ?? []);
        Assert.True(searched.IsSelected);
        Assert.Equal(chosenIdentity, searched.SelectedFilterVariantIdentity);
        Assert.Single(searched.FilterVariants, option => option.Identity == chosenIdentity);
    }

    [Fact]
    public void ModifierFilterVariant_IncompatibleBoundsRetainTextAndRestoreItWhenSwitchingBack()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.EffectiveDraftResolver = draft => draft with
        {
            ModifierFilters = draft.ModifierFilters.Select(modifier =>
                modifier.SelectedFilterVariantIdentity == "variant-presence"
                    ? modifier with
                    {
                        SupportsValueBounds = false,
                        RequestedMinimum = null,
                        RequestedMaximum = null,
                        ValueBoundsUnsupportedReason = "Presence-only filter.",
                    }
                    : modifier with
                    {
                        SupportsValueBounds = true,
                        ValueBoundsUnsupportedReason = null,
                    }).ToArray(),
        };
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [VariantModifier(includePresence: true)]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierBoundsChanged(0, "20.125", "27.875");
        Assert.Equal(
            "variant-crafted",
            Assert.Single(fixture.Window.CurrentState?.Draft.ModifierFilters ?? []).SelectedFilterVariantIdentity);

        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-presence");

        var presence = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.False(presence.CanEditBounds);
        Assert.Equal("20.125", presence.MinimumText);
        Assert.Equal("27.875", presence.MaximumText);
        Assert.Null(Assert.Single(fixture.Window.CurrentState?.Draft.ModifierFilters ?? []).RequestedMinimum);

        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-crafted");

        var restored = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.True(restored.CanEditBounds);
        Assert.Equal("20.125", restored.MinimumText);
        Assert.Equal("27.875", restored.MaximumText);
        var resolved = Assert.Single(fixture.Window.CurrentState?.Draft.ModifierFilters ?? []);
        Assert.Equal(20.125m, resolved.RequestedMinimum);
        Assert.Equal(27.875m, resolved.RequestedMaximum);
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task ModifierFilterVariant_ChangePreventsStaleSearchCompletionFromRestoringResults()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell", modifiers: [VariantModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        var activeSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-pseudo");
        completion.SetResult(SuccessResult([Offer("stale")], total: 1));
        await activeSearch;

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);
        Assert.True(fixture.PriceCheckService.Calls[0].CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void ModifierContributors_ExpandAndCollapseWithoutChangingStateOrRequests()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());

        var collapsed = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.False(collapsed.IsExpanded);
        Assert.True(collapsed.ShowsExpansionControl);
        Assert.Equal(2, collapsed.Contributors.Count);
        Assert.All(collapsed.Contributors, contributor => Assert.False(contributor.IsSelected));

        fixture.Window.RaiseModifierExpansionChanged(0, isExpanded: true);

        var expanded = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.True(expanded.IsExpanded);
        Assert.Equal(["30% increased Physical Damage", "116% increased Physical Damage"],
            expanded.Contributors.Select(contributor => contributor.Text));
        Assert.Equal(["30", "116"], expanded.Contributors.Select(contributor => contributor.MinimumText));

        fixture.Window.RaiseModifierExpansionChanged(0, isExpanded: false);

        var recollapsed = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.False(recollapsed.IsExpanded);
        Assert.Equal(["30", "116"], recollapsed.Contributors.Select(contributor => contributor.MinimumText));
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task ModifierContributors_ExpansionPreservesSuccessfulResultsAndTradeIdentity()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("id-1")], total: 1);
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();
        var successfulState = fixture.Window.CurrentSearchState!;

        fixture.Window.RaiseModifierExpansionChanged(0, isExpanded: true);
        fixture.Window.RaiseModifierExpansionChanged(0, isExpanded: false);

        var collapsedAgain = fixture.Window.CurrentSearchState!;
        Assert.Equal(PriceCheckerSearchViewStatus.Success, collapsedAgain.Status);
        Assert.Equal(successfulState.Offers, collapsedAgain.Offers);
        Assert.True(collapsedAgain.CanOpenTrade);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public void ModifierContributor_SelectionSelectsParentAndParentDeselectionClearsChildrenWithoutLocking()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier(parentMinimum: 10m)]),
            ValidationSuccess());

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);

        var selected = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.True(selected.IsSelected);
        Assert.True(selected.Contributors[0].IsSelected);
        Assert.Equal("30", selected.MinimumText);
        Assert.Contains("1 selected", selected.SectionLabel, StringComparison.Ordinal);

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);

        var deselected = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.False(deselected.IsSelected);
        Assert.All(deselected.Contributors, contributor => Assert.False(contributor.IsSelected));
        Assert.Equal("146", deselected.MinimumText);

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        var reselected = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.True(reselected.IsSelected);
        Assert.All(reselected.Contributors, contributor => Assert.False(contributor.IsSelected));

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: false);

        var childDeselected = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.True(childDeselected.IsSelected);
        Assert.False(childDeselected.Contributors[0].IsSelected);
        Assert.Equal("146", childDeselected.MinimumText);
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public void ModifierContributor_LastExplicitChildSelectionReplacesManualParentMinimum()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier(parentMinimum: 0m)]),
            ValidationSuccess());
        Assert.Equal("146", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        Assert.Equal("30", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 1, isSelected: true);
        Assert.Equal("146", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: false);
        Assert.Equal("116", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 1, isSelected: false);
        Assert.Equal("146", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);

        var secondOnlyFixture = SearchFixture.Create();
        secondOnlyFixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier(parentMinimum: 0m)]),
            ValidationSuccess());
        secondOnlyFixture.Window.RaiseModifierContributorSelectionChanged(0, 1, isSelected: true);
        Assert.Equal(
            "116",
            Assert.Single(secondOnlyFixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);

        var higherFixture = SearchFixture.Create();
        higherFixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());
        higherFixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        higherFixture.Window.RaiseModifierBoundsChanged(0, "200", string.Empty);
        higherFixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        Assert.Equal("30", Assert.Single(higherFixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);

        higherFixture.Window.RaiseModifierBoundsChanged(0, "20", string.Empty);
        var manuallyLowered = Assert.Single(higherFixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.Equal("20", manuallyLowered.MinimumText);
        Assert.True(manuallyLowered.Contributors[0].IsSelected);
        Assert.True(manuallyLowered.Contributors[0].IsInactive);

        higherFixture.Window.RaiseModifierContributorSelectionChanged(0, 1, isSelected: true);
        var childDerivedAgain = Assert.Single(higherFixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.Equal("146", childDerivedAgain.MinimumText);
        Assert.All(childDerivedAgain.Contributors, contributor => Assert.False(contributor.IsInactive));

        var editedChildrenFixture = SearchFixture.Create();
        editedChildrenFixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());
        editedChildrenFixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        editedChildrenFixture.Window.RaiseModifierContributorSelectionChanged(0, 1, isSelected: true);
        editedChildrenFixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "50", string.Empty);
        editedChildrenFixture.Window.RaiseModifierContributorBoundsChanged(0, 1, "45", string.Empty);
        Assert.Equal(
            "95",
            Assert.Single(editedChildrenFixture.Window.CurrentSearchState?.Modifiers ?? []).MinimumText);
    }

    [Fact]
    public void ModifierContributor_ArmageddonSecondChildImmediatelyReplacesUntouchedCanonicalMinimum()
    {
        var fixture = SearchFixture.Create();
        var source = ContributorModifier(parentMinimum: 91m);
        var contributors = source.Contributors
            .Select((contributor, index) => contributor with
            {
                RequestedMinimum = index == 0 ? 52m : 39m,
                Source = contributor.Source with
                {
                    OriginalText = index == 0
                        ? "52% increased Physical Damage"
                        : "39% increased Physical Damage",
                    ObservedNumericValues = [index == 0 ? 52m : 39m],
                    CanonicalNumericValues = [index == 0 ? 52m : 39m],
                },
            })
            .ToArray();
        var armageddon = source with
        {
            OriginalText = "91% increased Physical Damage",
            RequestedMinimum = 91m,
            ObservedNumericValues = [91m],
            CanonicalNumericValues = [91m],
            Sources = contributors.Select(contributor => contributor.Source).ToArray(),
            Contributors = contributors,
        };
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armageddon Thirst", modifiers: [armageddon]),
            ValidationSuccess());

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 1, isSelected: true);

        var row = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.Equal("39", row.MinimumText);
        Assert.True(row.Contributors[1].IsSelected);
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public void ModifierContributor_BoundsSurviveCollapseAndReselectionWithoutReplacingRowsOrRequests()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        var rowsDuringEdit = fixture.Window.CurrentSearchState!.Modifiers;
        var parentDuringEdit = Assert.Single(rowsDuringEdit);
        var childDuringEdit = parentDuringEdit.Contributors[0];
        var changedProperties = new List<string?>();
        parentDuringEdit.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);
        var updateCount = fixture.Window.ModifierCollections.Count;
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "3", string.Empty);
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "35", "4");
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "-", "40");
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "2,", "40");
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "35.5", "40");

        Assert.Same(rowsDuringEdit, fixture.Window.CurrentSearchState?.Modifiers);
        Assert.Same(parentDuringEdit, Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []));
        Assert.Same(childDuringEdit, parentDuringEdit.Contributors[0]);
        Assert.All(
            fixture.Window.ModifierCollections.Skip(updateCount),
            collection => Assert.Same(rowsDuringEdit, collection));
        Assert.Equal("35.5", parentDuringEdit.MinimumText);
        Assert.Contains(nameof(PriceCheckerModifierViewModel.MinimumText), changedProperties);

        fixture.Window.RaiseModifierExpansionChanged(0, isExpanded: true);
        fixture.Window.RaiseModifierExpansionChanged(0, isExpanded: false);
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: false);
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);

        var parent = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        var child = parent.Contributors[0];
        Assert.True(child.IsSelected);
        Assert.Equal("35.5", child.MinimumText);
        Assert.Equal("40", child.MaximumText);
        Assert.Equal("35.5", parent.MinimumText);
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public void ModifierContributor_ParentTypeDisablesWithoutClearingAndPseudoRestoresStateAndFloor()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.EffectiveDraftResolver = draft => draft with
        {
            ModifierFilters = draft.ModifierFilters.Select(modifier => modifier with
            {
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                ProviderStatId = modifier.SelectedFilterVariantIdentity == "variant-parent-fractured"
                    ? "fractured.physical"
                    : "pseudo.total-physical",
                ProviderStatText = "#% increased Physical Damage",
            }).ToArray(),
        };
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "35", "40");

        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-parent-fractured");

        var inactiveParent = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.Equal("35", inactiveParent.MinimumText);
        Assert.Equal(0, inactiveParent.ActiveContributorCount);
        Assert.True(inactiveParent.Contributors[0].IsSelected);
        Assert.True(inactiveParent.Contributors[0].IsInactive);
        Assert.False(inactiveParent.Contributors[0].IsInteractionEnabled);
        Assert.False(inactiveParent.Contributors[0].CanEditBounds);
        Assert.DoesNotContain("selected", inactiveParent.SectionLabel, StringComparison.Ordinal);

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: false);
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "99", "100");
        Assert.True(Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).Contributors[0].IsSelected);

        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-parent-pseudo");

        var restoredParent = Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []);
        Assert.Equal("35", restoredParent.MinimumText);
        Assert.Equal(1, restoredParent.ActiveContributorCount);
        Assert.True(restoredParent.Contributors[0].IsSelected);
        Assert.True(restoredParent.Contributors[0].IsInteractionEnabled);
        Assert.Equal("35", restoredParent.Contributors[0].MinimumText);
        Assert.Equal("40", restoredParent.Contributors[0].MaximumText);
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task ModifierContributor_InvalidRetainedChildDoesNotBlockNonPseudoParentButBlocksWhenPseudoReturns()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.EffectiveDraftResolver = draft => draft with
        {
            ModifierFilters = draft.ModifierFilters.Select(modifier => modifier with
            {
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                ProviderStatId = modifier.SelectedFilterVariantIdentity == "variant-parent-fractured"
                    ? "fractured.physical"
                    : "pseudo.total-physical",
                ProviderStatText = "#% increased Physical Damage",
            }).ToArray(),
        };
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        fixture.Window.RaiseModifierContributorBoundsChanged(0, 0, "-", string.Empty);
        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-parent-fractured");

        await fixture.Controller.SearchAsync();

        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.NotEqual(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("-", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).Contributors[0].MinimumText);

        fixture.Window.RaiseModifierFilterVariantChanged(0, "variant-parent-pseudo");
        await fixture.Controller.SearchAsync();

        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Contributor Min and Max must be finite decimal numbers.", fixture.Window.CurrentSearchState?.Message);
        Assert.Equal("-", Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).Contributors[0].MinimumText);
    }

    [Fact]
    public void ModifierContributor_MissingRetainedSourceIdentityProducesPreciseDeveloperDiagnostic()
    {
        var fixture = SearchFixture.Create();
        var parent = ContributorModifier() with
        {
            Contributors = ContributorModifier().Contributors
                .Select((contributor, index) => index == 0
                    ? contributor with
                    {
                        ProviderResolutionStatus = SearchComponentProviderResolutionStatus.NotFound,
                        ProviderIdentity = null,
                        ProviderDiagnosticMessage =
                            "Contributor '30% increased Physical Damage' has no exact retained source provider identity.",
                    }
                    : contributor)
                .ToArray(),
        };
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [parent]),
            ValidationSuccess());

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);

        Assert.Contains(fixture.Controller.CurrentDeveloperDiagnostics.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.InvalidContributorSourceIdentity &&
            diagnostic.Message.Contains("30% increased Physical Damage", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("retained source provider identity", StringComparison.Ordinal));
        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task ModifierContributor_ChangeInvalidatesSuccessfulSearchWithoutAutomaticRequests()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [Offer("id-1")],
            total: 2,
            resultIds: ["id-1", "id-2"],
            fetchedResultIds: ["id-1"]);
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        await fixture.Controller.SearchAsync();

        Assert.Single(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.True(fixture.Window.CurrentSearchState?.CanOpenTrade);
        Assert.True(fixture.Window.CurrentSearchState?.CanLoadMore);

        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task ModifierContributor_ChangePreventsStaleSearchCompletionFromRestoringResults()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(
            Draft("Horror Mangler", modifiers: [ContributorModifier()]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        var activeSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        fixture.Window.RaiseModifierContributorSelectionChanged(0, 0, isSelected: true);
        completion.SetResult(SuccessResult([Offer("stale")], total: 1));
        await activeSearch;

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);
        Assert.True(fixture.PriceCheckService.Calls[0].CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task SearchAsync_CallsServiceOnceWithCurrentDraftValidationAndMirage()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Armoured Shell");
        var validation = ValidationSuccess();
        fixture.Controller.UpdateCurrentDraft(draft, validation);

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Same(draft, call.Draft);
        Assert.Same(validation, call.ValidationResult);
        Assert.Equal("Mirage", call.LeagueIdentifier);
    }

    [Fact]
    public async Task SearchAsync_MirageSessionDoesNotChangeListingModeOrQueryCriteria()
    {
        var fixture = SearchFixture.Create();
        var draft = Draft("Armoured Shell");
        fixture.Controller.UpdateCurrentDraft(draft, ValidationSuccess());
        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Same(draft, call.Draft);
        Assert.Equal(TradeListingMode.InstantBuyout, call.Draft?.ListingMode);
        Assert.Equal(ItemBaseResolutionStatus.Exact, call.Draft?.Base.Status);
        Assert.Equal("base.titan-plate", call.Draft?.Base.ResolvedBaseId);
        Assert.Equal("Rare", call.Draft?.Rarity);
    }

    [Fact]
    public async Task SearchAsync_InvalidDraftPreventsExecution()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft("Armoured Shell"),
            TradeSearchValidationResult.FromDiagnostics(
            [
                new TradeSearchValidationDiagnostic(
                    "LOCAL_INVALID",
                    TradeSearchValidationSeverity.Error,
                    "Invalid draft."),
            ]));

        await fixture.Controller.SearchAsync();

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal("Select a supported Trade search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_SelectedModifierCallsServiceWhenDraftIsLocallyValid()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.True(Assert.Single(call.Draft?.ModifierFilters ?? []).IsSelected);
        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
    }

    [Fact]
    public async Task SearchAsync_SelectedLocallyUnresolvedModifierUsesProviderMappingFailure()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.ModifierMapping,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed,
                    "Not found.",
                    PathOfExileTradePriceCheckStage.ModifierMapping,
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life", resolvedModifierId: null)]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        await fixture.Controller.SearchAsync();

        var call = Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Contains(
            call.ValidationResult?.Diagnostics ?? [],
            diagnostic =>
                diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved &&
                diagnostic.Severity == TradeSearchValidationSeverity.Warning);
        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Selected modifier is not available in Trade search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public void ModifierSelectionChanged_LocallyUnresolvedModifierKeepsSearchEnabledBeforeProviderMapping()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life", resolvedModifierId: null)]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.True(fixture.Window.CurrentSearchState?.CanSearch);
        Assert.Equal("Ready to search.", fixture.Window.CurrentSearchState?.Message);
        Assert.Contains(
            fixture.Window.CurrentState?.ValidationResult.Diagnostics ?? [],
            diagnostic =>
                diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved &&
                diagnostic.Severity == TradeSearchValidationSeverity.Warning);
    }

    [Fact]
    public void UpdateCurrentDraft_DisplaysModifiersInDraftOrderUncheckedAndWithSectionLabels()
    {
        var fixture = SearchFixture.Create();

        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier("+12% to Fire Resistance", ParsedModifierKind.Implicit),
                    Modifier("+10 to maximum Life", ParsedModifierKind.Prefix),
                    Modifier("+20% to Cold Resistance", ParsedModifierKind.Suffix),
                ]),
            ValidationSuccess());

        var modifiers = fixture.Window.CurrentSearchState?.Modifiers ?? [];
        Assert.Equal(
            ["+12% to Fire Resistance", "+10 to maximum Life", "+20% to Cold Resistance"],
            modifiers.Select(modifier => modifier.Text));
        Assert.Equal(["Implicit", "Prefix", "Suffix"], modifiers.Select(modifier => modifier.SectionLabel));
        Assert.All(modifiers, modifier => Assert.False(modifier.IsSelected));
        Assert.Equal(0, fixture.Window.CurrentSearchState?.SelectedModifierCount);
    }

    [Fact]
    public void ModifierSelectionChanged_SelectsOnlyRequestedModifierUpdatesCountAndDoesNotCallService()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+20% to Cold Resistance"),
                    Modifier("+30% to Fire Resistance"),
                ]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(1, isSelected: true);

        Assert.Empty(fixture.PriceCheckService.Calls);
        Assert.Equal([false, true, false], fixture.Window.CurrentSearchState?.Modifiers.Select(modifier => modifier.IsSelected));
        Assert.Equal(1, fixture.Window.CurrentSearchState?.SelectedModifierCount);
        Assert.Equal([false, true, false], fixture.Window.CurrentState?.Draft.ModifierFilters.Select(modifier => modifier.IsSelected));
    }

    [Fact]
    public void ModifierSelectionChanged_UnselectingRestoresModifierToUnselected()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);

        Assert.Equal(0, fixture.Window.CurrentSearchState?.SelectedModifierCount);
        Assert.False(Assert.Single(fixture.Window.CurrentSearchState?.Modifiers ?? []).IsSelected);
    }

    [Fact]
    public void ModifierSelectionChanged_DuplicateTextRowsRemainIndependent()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+10 to maximum Life"),
                ]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(1, isSelected: true);

        var modifiers = fixture.Window.CurrentSearchState?.Modifiers ?? [];
        Assert.Equal(["+10 to maximum Life", "+10 to maximum Life"], modifiers.Select(modifier => modifier.Text));
        Assert.Equal([false, true], modifiers.Select(modifier => modifier.IsSelected));
        Assert.Equal([0, 1], modifiers.Select(modifier => modifier.SourceIndex));
    }

    [Fact]
    public async Task SearchAsync_SelectedModifiersPreserveDraftOrderInServiceDraft()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+20% to Cold Resistance"),
                    Modifier("+30% to Fire Resistance"),
                ]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(2, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        await fixture.Controller.SearchAsync();

        var draft = Assert.Single(fixture.PriceCheckService.Calls).Draft;
        Assert.NotNull(draft);
        var selected = draft.ModifierFilters
            .Select((modifier, index) => (modifier, index))
            .Where(pair => pair.modifier.IsSelected)
            .Select(pair => pair.index)
            .ToArray();
        Assert.Equal([0, 2], selected);
    }

    [Fact]
    public async Task SearchAsync_ZeroSelectionsPassesBaseOnlyDraft()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.DoesNotContain(
            Assert.Single(fixture.PriceCheckService.Calls).Draft?.ModifierFilters ?? [],
            modifier => modifier.IsSelected);
    }

    [Fact]
    public async Task SearchAsync_LoadingDisablesSearchAndRepeatedClickDoesNotStartSecondCall()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        var firstSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        var secondSearch = fixture.Controller.SearchAsync();

        Assert.False(fixture.Window.CurrentSearchState?.CanSearch);
        Assert.Equal(PriceCheckerSearchViewStatus.Loading, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Searching...", fixture.Window.CurrentSearchState?.Message);
        Assert.Single(fixture.PriceCheckService.Calls);

        completion.SetResult(SuccessResult([Offer("id-1")], total: 1));
        await firstSearch;
        await secondSearch;
    }

    [Fact]
    public async Task SearchAsync_SuccessDisplaysFetchedOffersInOrderCountTotalAndInexact()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [
                Offer("id-1", amount: 1.25m, currency: "divine"),
                Offer("id-2", amount: 10m, currency: "chaos"),
            ],
            total: 148,
            inexact: true);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var state = fixture.Window.CurrentSearchState;
        Assert.Equal(PriceCheckerSearchViewStatus.Success, state?.Status);
        Assert.Equal("Showing 2 of 148 offers (inexact)", state?.Summary);
        Assert.Equal(["1.25 divine", "10 chaos"], state?.Offers.Select(offer => offer.PriceText));
    }

    [Fact]
    public async Task TradeButton_IsDisabledUntilTheCurrentSearchSucceedsWithAQueryId()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);

        await fixture.Controller.SearchAsync();

        Assert.True(fixture.Window.CurrentSearchState?.CanOpenTrade);
    }

    [Fact]
    public async Task TradeButton_OpensTheSuccessfulSearchWithoutAnotherSearchOrFetch()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        fixture.Window.RaiseTradeRequested();

        var uri = Assert.Single(fixture.ExternalUrlLauncher.OpenedUris);
        Assert.Equal("https://www.pathofexile.com/trade/search/Mirage/query-1", uri.AbsoluteUri);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task TradeButton_LoadMoreKeepsTheSameLinkEnabled()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 11).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 11,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(10)),
            total: 11,
            fetchedResultIds: ids.Skip(10).ToArray()));
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        await fixture.Controller.LoadMoreAsync();
        fixture.Window.RaiseTradeRequested();

        Assert.True(fixture.Window.CurrentSearchState?.CanOpenTrade);
        Assert.Equal(
            "https://www.pathofexile.com/trade/search/Mirage/query-1",
            Assert.Single(fixture.ExternalUrlLauncher.OpenedUris).AbsoluteUri);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Single(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task TradeButton_QueryRelevantChangesInvalidateTheSuccessfulSearchLink()
    {
        var fixture = SearchFixture.Create();
        var draft = DraftWithBothBaseCriteria() with
        {
            ModifierFilters = [Modifier("+10 to maximum Life")],
        };
        fixture.Controller.UpdateCurrentDraft(draft, ValidationSuccess());
        await fixture.Controller.SearchAsync();
        Assert.True(fixture.Window.CurrentSearchState?.CanOpenTrade);

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);

        await fixture.Controller.SearchAsync();
        fixture.Window.RaiseBaseCriterionToggleRequested();
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);

        await fixture.Controller.SearchAsync();
        fixture.Controller.UpdateCurrentDraft(Draft("New Ctrl+D item"), ValidationSuccess());
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);
    }

    [Fact]
    public async Task TradeButton_FailedOrCancelledSearchAndStaleCompletionCannotEnableIt()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        fixture.PriceCheckService.Result = FailureResult("Failed.");
        await fixture.Controller.SearchAsync();
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);

        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Search,
            IsCancelled = true,
        };
        await fixture.Controller.SearchAsync();
        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);

        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        var staleSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 3);
        fixture.Controller.UpdateCurrentDraft(Draft("New Ctrl+D item"), ValidationSuccess());
        completion.SetResult(SuccessResult([Offer("late")], total: 1));
        await staleSearch;

        Assert.False(fixture.Window.CurrentSearchState?.CanOpenTrade);
    }

    [Fact]
    public async Task TradeButton_BrowserFailurePreservesSuccessfulResults()
    {
        var fixture = SearchFixture.Create();
        fixture.ExternalUrlLauncher.ShouldOpen = false;
        fixture.PriceCheckService.Result = SuccessResult([Offer("id-1")], total: 1);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        fixture.Window.RaiseTradeRequested();

        Assert.Equal("Could not open Trade in your browser.", fixture.Window.CurrentSearchState?.Message);
        Assert.Single(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.True(fixture.Window.CurrentSearchState?.CanOpenTrade);
        Assert.Empty(fixture.ExternalUrlLauncher.OpenedUris);
    }

    [Fact]
    public async Task OfferCapacity_OfTwentyThreeFetchesTenThenTenThenThreeWithoutAnotherSearch()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 30).Select(index => $"id-{index}").ToArray();
        fixture.Window.RaiseOfferCapacityChanged(23);
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 30,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(10).Take(10).Reverse()),
            total: 30,
            fetchedResultIds: ids.Skip(10).Take(10).ToArray()));
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(20).Take(3).Reverse()),
            total: 30,
            fetchedResultIds: ids.Skip(20).Take(3).ToArray()));
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();
        await fixture.Controller.LoadMoreAsync();
        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(10, fixture.PriceCheckService.Calls.Single().InitialFetchResultCount);
        Assert.Equal(23, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.Equal(ids.Skip(20).Take(3), fixture.PriceCheckService.LoadMoreCalls[1].ResultIds);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Single(fixture.PriceCheckService.Calls);
    }

    [Fact]
    public async Task OfferCapacity_OfSixteenFetchesTenThenExactlySix()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 20).Select(index => $"id-{index}").ToArray();
        fixture.Window.RaiseOfferCapacityChanged(16);
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 20,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(10).Take(6)),
            total: 20,
            fetchedResultIds: ids.Skip(10).Take(6).ToArray()));
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();
        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(16, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.Equal(ids.Skip(10).Take(6), Assert.Single(fixture.PriceCheckService.LoadMoreCalls).ResultIds);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
    }

    [Fact]
    public async Task OfferCapacity_BelowTenNeverFetchesOrDisplaysMoreThanFits()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.Window.RaiseOfferCapacityChanged(6);
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(6)),
            total: 12,
            resultIds: ids,
            fetchedResultIds: ids.Take(6).ToArray());
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(6, fixture.PriceCheckService.Calls.Single().InitialFetchResultCount);
        Assert.Equal(6, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task OfferCapacity_ZeroHidesLoadMoreAndDoesNotFetchOffers()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.Window.RaiseOfferCapacityChanged(0);
        fixture.PriceCheckService.Result = SuccessResult(
            [],
            total: 12,
            resultIds: ids,
            fetchedResultIds: []);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();
        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(0, fixture.PriceCheckService.Calls.Single().InitialFetchResultCount);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task OfferCapacity_NewItemUsesItsOwnMeasuredCapacity()
    {
        var fixture = SearchFixture.Create();
        fixture.Window.RaiseOfferCapacityChanged(23);
        fixture.Controller.UpdateCurrentDraft(Draft("First item"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        fixture.Window.RaiseOfferCapacityChanged(6);
        fixture.Controller.UpdateCurrentDraft(Draft("New Ctrl+D item"), ValidationSuccess());
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(Enumerable.Range(101, 6).Select(index => $"id-{index}")),
            total: 6,
            resultIds: Enumerable.Range(101, 6).Select(index => $"id-{index}").ToArray(),
            fetchedResultIds: Enumerable.Range(101, 6).Select(index => $"id-{index}").ToArray());

        await fixture.Controller.SearchAsync();

        Assert.Equal(6, fixture.PriceCheckService.Calls[1].InitialFetchResultCount);
        Assert.Equal(6, fixture.Window.CurrentSearchState?.Offers.Count);
    }

    [Fact]
    public async Task SearchAsync_MapsOneFetchedOfferToFourStructuredColumns()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [Offer(
                "id-1",
                amount: 3m,
                currency: "chaos",
                accountName: "Seller Account",
                itemName: "Armageddon Thirst",
                itemLevel: 72,
                indexed: DateTimeOffset.UtcNow.AddSeconds(-30))],
            total: 1);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var offer = Assert.Single(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.Equal("Armageddon Thirst", offer.ItemName);
        Assert.Equal("Seller Account", offer.SellerAccountName);
        Assert.Equal("1 min ago", offer.ListedText);
        Assert.Equal("72", offer.ItemLevelText);
        Assert.Equal("3 chaos", offer.PriceText);
    }

    [Fact]
    public async Task SearchAsync_OfferDisplayNamePrefersNameThenTypeLineThenBaseType()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [
                Offer(
                    "named",
                    accountName: "Named seller",
                    itemName: "Armageddon Thirst",
                    typeLine: "Reaver Axe",
                    baseType: "Axe"),
                Offer(
                    "magic",
                    accountName: "Magic seller",
                    itemName: " ",
                    typeLine: "Sapphire Ring of Rejuvenation",
                    baseType: "Sapphire Ring"),
                Offer(
                    "normal",
                    accountName: "Normal seller",
                    itemName: null,
                    typeLine: "",
                    baseType: "Iron Ring"),
                Offer(
                    "missing",
                    accountName: "Missing seller",
                    itemName: null,
                    typeLine: null,
                    baseType: null),
            ],
            total: 4);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var offers = fixture.Window.CurrentSearchState?.Offers ?? [];
        Assert.Equal("Armageddon Thirst", offers[0].ItemName);
        Assert.Equal("Sapphire Ring of Rejuvenation", offers[1].ItemName);
        Assert.Equal("Magic seller", offers[1].SellerAccountName);
        Assert.Equal("Iron Ring", offers[2].ItemName);
        Assert.Equal("—", offers[3].ItemName);
    }

    [Fact]
    public async Task SearchAsync_DoesNotDisplayDuplicateFetchedResultIds()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [Offer("id-1", amount: 1m), Offer("id-2", amount: 2m)],
            total: 2,
            resultIds: ["id-1", "id-1", "id-2"],
            fetchedResultIds: ["id-1", "id-1", "id-2"]);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(
            ["id-1", "id-2"],
            fixture.Window.CurrentSearchState?.Offers.Select(offer => offer.Id));
    }

    [Fact]
    public async Task LoadMoreAsync_FetchesSuccessiveBatchesWithoutAnotherSearchAndAppendsProviderOrder()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 25).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 25,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(10).Take(10).Reverse()),
            total: 25,
            fetchedResultIds: ids.Skip(10).Take(10).ToArray()));
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(20).Reverse()),
            total: 25,
            fetchedResultIds: ids.Skip(20).ToArray()));
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.True(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Single(fixture.PriceCheckService.Calls);
        fixture.Window.RaiseLoadMoreRequested();
        await WaitUntilAsync(() =>
            fixture.PriceCheckService.LoadMoreCalls.Count == 1 &&
            fixture.Window.CurrentSearchState?.Offers.Count == 20);

        var firstLoadMore = Assert.Single(fixture.PriceCheckService.LoadMoreCalls);
        Assert.Equal("query-1", firstLoadMore.SearchQueryId);
        Assert.Equal(ids.Skip(10).Take(10), firstLoadMore.ResultIds);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Equal(
            Enumerable.Range(1, 20).Select(index => $"{index} chaos"),
            fixture.Window.CurrentSearchState?.Offers.Select(offer => offer.PriceText));

        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(2, fixture.PriceCheckService.LoadMoreCalls.Count);
        Assert.Equal(ids.Skip(20), fixture.PriceCheckService.LoadMoreCalls[1].ResultIds);
        Assert.Equal(25, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Equal(
            25,
            ids.Take(10)
                .Concat(fixture.PriceCheckService.LoadMoreCalls
                .SelectMany(call => call.ResultIds ?? [])
                )
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public async Task LoadMoreAsync_FailurePreservesOffersAndLeavesTheSameBatchForExplicitRetry()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 12,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Fetch,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed,
                    "Fetch failed.",
                    PathOfExileTradePriceCheckStage.Fetch),
            ],
        });
        fixture.PriceCheckService.PendingLoadMoreResults.Enqueue(SuccessResult(
            OffersFor(ids.Skip(10)),
            total: 12,
            fetchedResultIds: ids.Skip(10).ToArray()));
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(10, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.True(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Equal("Could not load more offers. Try again.", fixture.Window.CurrentSearchState?.Message);
        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(ids.Skip(10), fixture.PriceCheckService.LoadMoreCalls[0].ResultIds);
        Assert.Equal(ids.Skip(10), fixture.PriceCheckService.LoadMoreCalls[1].ResultIds);
        Assert.Equal(12, fixture.Window.CurrentSearchState?.Offers.Count);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
    }

    [Fact]
    public async Task SearchAsync_CancelsActiveLoadMoreAndPreventsItsLateCompletionFromAppending()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 20).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 20,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.Controller.UpdateCurrentDraft(Draft("First"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.LoadMoreHandler = _ => completion.Task;
        var loadMore = fixture.Controller.LoadMoreAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.LoadMoreCalls.Count == 1);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        fixture.PriceCheckService.Result = SuccessResult([Offer("new-id", amount: 99m)], total: 1);

        await fixture.Controller.SearchAsync();

        Assert.True(fixture.PriceCheckService.LoadMoreCalls[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(["99 chaos"], fixture.Window.CurrentSearchState?.Offers.Select(offer => offer.PriceText));
        completion.SetResult(SuccessResult(OffersFor(ids.Skip(10)), total: 20));
        await loadMore;

        Assert.Equal(["99 chaos"], fixture.Window.CurrentSearchState?.Offers.Select(offer => offer.PriceText));
        Assert.Equal(2, fixture.PriceCheckService.Calls.Count);
        Assert.Empty(fixture.PriceCheckService.PendingLoadMoreResults);
    }

    [Fact]
    public async Task UpdateCurrentDraft_ClearsPaginationAndMakesLoadMoreUnavailable()
    {
        var fixture = SearchFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.PriceCheckService.Result = SuccessResult(
            OffersFor(ids.Take(10)),
            total: 12,
            resultIds: ids,
            fetchedResultIds: ids.Take(10).ToArray());
        fixture.Controller.UpdateCurrentDraft(Draft("First"), ValidationSuccess());
        await fixture.Controller.SearchAsync();

        fixture.Controller.UpdateCurrentDraft(Draft("Second"), ValidationSuccess());
        await fixture.Controller.LoadMoreAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.Empty(fixture.PriceCheckService.LoadMoreCalls);
    }

    [Fact]
    public async Task SearchAsync_ZeroResultSuccessDisplaysNoOffersFound()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([], total: 0);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("No offers found.", fixture.Window.CurrentSearchState?.Message);
        Assert.Empty(fixture.Window.CurrentSearchState?.Summary ?? string.Empty);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.False(fixture.Window.CurrentSearchState?.CanLoadMore);
    }

    [Fact]
    public async Task SearchAsync_RepeatedZeroResultsKeepOneMessageAndNoStaleOffers()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("old")], total: 1);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());
        await fixture.Controller.SearchAsync();
        Assert.NotEmpty(fixture.Window.CurrentSearchState?.Offers ?? []);

        fixture.PriceCheckService.Result = SuccessResult([], total: 0);
        await fixture.Controller.SearchAsync();
        await fixture.Controller.SearchAsync();

        var state = fixture.Window.CurrentSearchState;
        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, state?.Status);
        Assert.Equal("No offers found.", state?.Message);
        Assert.Empty(state?.Summary ?? string.Empty);
        Assert.Empty(state?.Offers ?? []);
    }

    [Fact]
    public async Task SearchAsync_OfferRowsKeepFieldsSeparateAndUseMissingMarkers()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult(
            [
                Offer(
                    "id-1",
                    amount: null,
                    currency: null,
                    lastCharacterName: "LastChar",
                    accountName: "Account",
                    rawIndexed: "raw-indexed",
                    itemName: "Named item",
                    itemLevel: 85),
                Offer(
                    "id-2",
                    amount: 3m,
                    currency: "divine",
                    lastCharacterName: null,
                    accountName: "AccountOnly"),
                Offer(
                    "id-3",
                    amount: 7.5m,
                    currency: "chaos",
                    lastCharacterName: null,
                    accountName: null,
                    itemName: null,
                    typeLine: null,
                    baseType: null,
                    itemLevel: null),
            ],
            total: 3);
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var offers = fixture.Window.CurrentSearchState?.Offers ?? [];
        Assert.Equal("—", offers[0].PriceText);
        Assert.Equal("Named item", offers[0].ItemName);
        Assert.Equal("Account", offers[0].SellerAccountName);
        Assert.Equal("—", offers[0].ListedText);
        Assert.Equal("raw-indexed", offers[0].ListedToolTip);
        Assert.Equal("85", offers[0].ItemLevelText);
        Assert.Equal("AccountOnly", offers[1].SellerAccountName);
        Assert.Equal("—", offers[2].ItemName);
        Assert.Equal("—", offers[2].SellerAccountName);
        Assert.Equal("—", offers[2].ItemLevelText);
    }

    [Fact]
    public async Task UpdateCurrentDraft_CancelsActiveRequestClearsOldOffersAndPreventsLateOverwrite()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("old")], total: 1);
        fixture.Controller.UpdateCurrentDraft(Draft("Old Loop"), ValidationSuccess());
        await fixture.Controller.SearchAsync();
        Assert.NotEmpty(fixture.Window.CurrentSearchState?.Offers ?? []);

        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        var activeSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 2);

        fixture.Controller.UpdateCurrentDraft(Draft("New Loop"), ValidationSuccess());

        Assert.True(fixture.PriceCheckService.Calls[1].CancellationToken.IsCancellationRequested);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);

        completion.SetResult(SuccessResult([Offer("late-old")], total: 1));
        await activeSearch;

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
    }

    [Fact]
    public async Task WindowClose_CancelsActiveRequestAndPreventsLateUiUpdate()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        var activeSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        fixture.Window.Close();

        Assert.True(fixture.PriceCheckService.Calls[0].CancellationToken.IsCancellationRequested);
        completion.SetResult(FailureResult("Provider exploded."));
        await activeSearch;

        Assert.NotEqual(PriceCheckerSearchViewStatus.ProviderOrTransportError, fixture.Window.CurrentSearchState?.Status);
    }

    [Fact]
    public async Task SearchAsync_CancellationResultDoesNotDisplayProviderFailure()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Search,
            IsCancelled = true,
        };
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.Cancelled, fixture.Window.CurrentSearchState?.Status);
        Assert.NotEqual("Trade request failed. Try again later.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_FailureDisplaysSafeConciseProviderMessage()
    {
        var fixture = SearchFixture.Create();
        var longProviderMessage = $"Provider said no.{Environment.NewLine}{new string('x', 240)}";
        fixture.PriceCheckService.Result = FailureResult(
            longProviderMessage,
            sourceCode: PathOfExileTradeHttpDiagnosticCodes.ProviderDeclaredError,
            providerCode: "3");
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        var message = fixture.Window.CurrentSearchState?.Message ?? string.Empty;
        Assert.Equal(PriceCheckerSearchViewStatus.ProviderOrTransportError, fixture.Window.CurrentSearchState?.Status);
        Assert.StartsWith("Trade returned an error: Provider said no.", message, StringComparison.Ordinal);
        Assert.NotEqual("No offers found.", fixture.Window.CurrentSearchState?.Message);
        Assert.NotEqual("No offers found.", fixture.Window.CurrentSearchState?.Summary);
        Assert.DoesNotContain(Environment.NewLine, message);
        Assert.True(message.Length <= 190);
    }

    [Fact]
    public async Task SearchAsync_CatalogFailureUsesTradeDefinitionsMessage()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.CatalogLoad,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed,
                    "Stats failed.",
                    PathOfExileTradePriceCheckStage.CatalogLoad),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell", selectedModifier: true), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ProviderOrTransportError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Could not load Trade modifier definitions.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_AmbiguousSelectedModifierUsesSafeMappingMessage()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.ModifierMapping,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed,
                    "Ambiguous.",
                    PathOfExileTradePriceCheckStage.ModifierMapping,
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell", selectedModifier: true), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Selected modifier matches multiple Trade filters.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task SearchAsync_UnmatchedSelectedModifierUsesSafeMappingMessage()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.ModifierMapping,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed,
                    "Not found.",
                    PathOfExileTradePriceCheckStage.ModifierMapping,
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound),
            ],
        };
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell", selectedModifier: true), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(PriceCheckerSearchViewStatus.ValidationError, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Selected modifier is not available in Trade search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task ModifierSelectionChanged_ClearsOldOffersAndProviderErrorWithoutCallingService()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.Result = SuccessResult([Offer("old")], total: 1);
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());
        await fixture.Controller.SearchAsync();
        Assert.NotEmpty(fixture.Window.CurrentSearchState?.Offers ?? []);
        var callsAfterSuccess = fixture.PriceCheckService.Calls.Count;

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Equal(callsAfterSuccess, fixture.PriceCheckService.Calls.Count);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Ready to search.", fixture.Window.CurrentSearchState?.Message);

        fixture.PriceCheckService.Result = FailureResult("Provider exploded.");
        await fixture.Controller.SearchAsync();
        Assert.Equal(PriceCheckerSearchViewStatus.ProviderOrTransportError, fixture.Window.CurrentSearchState?.Status);
        var callsAfterFailure = fixture.PriceCheckService.Calls.Count;

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);

        Assert.Equal(callsAfterFailure, fixture.PriceCheckService.Calls.Count);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Equal("Ready to search.", fixture.Window.CurrentSearchState?.Message);
    }

    [Fact]
    public async Task ModifierSelectionChanged_DuringLoadingCancelsRequestAndPreventsLateOverwrite()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<PathOfExileTradePriceCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.Handler = _ => completion.Task;
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());

        var activeSearch = fixture.Controller.SearchAsync();
        await WaitUntilAsync(() => fixture.PriceCheckService.Calls.Count == 1);
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.True(fixture.PriceCheckService.Calls[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.True(fixture.Window.CurrentSearchState?.CanSearch);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);

        completion.SetResult(SuccessResult([Offer("late-old")], total: 1));
        await activeSearch;

        Assert.Equal(PriceCheckerSearchViewStatus.Idle, fixture.Window.CurrentSearchState?.Status);
        Assert.Empty(fixture.Window.CurrentSearchState?.Offers ?? []);
    }

    [Fact]
    public void UpdateCurrentDraft_ReplacesModifierListAndClearsPriorSelectionsEvenForIdenticalText()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "First Loop",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+20% to Cold Resistance"),
                ]),
            ValidationSuccess());
        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);
        Assert.Equal(1, fixture.Window.CurrentSearchState?.SelectedModifierCount);

        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Second Loop",
                modifiers:
                [
                    Modifier("+10 to maximum Life"),
                    Modifier("+30% to Fire Resistance"),
                    Modifier("+40% to Lightning Resistance"),
                ]),
            ValidationSuccess());

        var modifiers = fixture.Window.CurrentSearchState?.Modifiers ?? [];
        Assert.Equal(
            ["+10 to maximum Life", "+30% to Fire Resistance", "+40% to Lightning Resistance"],
            modifiers.Select(modifier => modifier.Text));
        Assert.All(modifiers, modifier => Assert.False(modifier.IsSelected));
        Assert.Equal(0, fixture.Window.CurrentSearchState?.SelectedModifierCount);
    }

    [Fact]
    public void ModifierSelectionChanged_PreservesMirageAndPinState()
    {
        var fixture = SearchFixture.Create();
        fixture.Window.SetPinned(true);
        fixture.Controller.UpdateCurrentDraft(
            Draft(
                "Armoured Shell",
                modifiers: [Modifier("+10 to maximum Life")]),
            ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Equal("Mirage", fixture.Window.CurrentSearchState?.LeagueIdentifier);
        Assert.True(fixture.Window.IsPinned);
    }

    [Fact]
    public async Task SearchAsync_SearchBecomesAvailableAgainAfterCompletionWhenInputIsValid()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(Draft("Armoured Shell"), ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.True(fixture.Window.CurrentSearchState?.CanSearch);
    }

    [Fact]
    public void UpdateCurrentDraft_PreservesMirageDuringSameWindowItemChanges()
    {
        var fixture = SearchFixture.Create();

        fixture.Controller.UpdateCurrentDraft(Draft("First Loop"), ValidationSuccess());
        fixture.Controller.UpdateCurrentDraft(Draft("Second Loop"), ValidationSuccess());

        Assert.Equal("Mirage", fixture.Window.CurrentSearchState?.LeagueIdentifier);
    }

    [Fact]
    public async Task BaseCriterionToggleRequested_SwitchesActualDraftBetweenCategoryAndExactBase()
    {
        var fixture = SearchFixture.Create();
        fixture.Controller.UpdateCurrentDraft(DraftWithBothBaseCriteria(), ValidationSuccess());

        fixture.Window.RaiseBaseCriterionToggleRequested();

        Assert.Equal(
            BaseSearchMode.ExactBase,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
        await fixture.Controller.SearchAsync();
        Assert.Equal(
            BaseSearchMode.ExactBase,
            Assert.Single(fixture.PriceCheckService.Calls).Draft?.Base.ActiveCriterion?.Mode);

        fixture.Window.RaiseBaseCriterionToggleRequested();

        Assert.Equal(
            BaseSearchMode.Category,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
    }

    [Fact]
    public async Task SearchAsync_StygianForcedExactBaseResultRemainsReflectedInTheCurrentDraft()
    {
        var fixture = SearchFixture.Create();
        var categoryDraft = DraftWithBothBaseCriteria("Belt", "Stygian Vise");
        var forcedExactBase = categoryDraft with
        {
            Base = categoryDraft.Base with
            {
                ActiveCriterion = categoryDraft.Base.AvailableCriteria.ExactBase,
            },
        };
        fixture.PriceCheckService.Result = SuccessResult([], 0) with
        {
            EffectiveDraft = forcedExactBase,
        };
        fixture.Controller.UpdateCurrentDraft(categoryDraft, ValidationSuccess());

        await fixture.Controller.SearchAsync();

        Assert.Equal(
            BaseSearchMode.ExactBase,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
    }

    [Fact]
    public void ModifierSelection_ImmediatelyUsesTheProviderEffectiveDraftAndRestoresTheUserCategory()
    {
        var fixture = SearchFixture.Create();
        var categoryDraft = DraftWithBothBaseCriteria("Belt", "Stygian Vise") with
        {
            ModifierFilters =
            [
                Modifier("Has 1 Abyssal Socket", ParsedModifierKind.Implicit) with
                {
                    IsBaseImplicit = true,
                },
            ],
        };
        fixture.PriceCheckService.EffectiveDraftResolver = draft =>
        {
            var activeCriterion = draft.ModifierFilters.Any(modifier => modifier.IsSelected && modifier.IsBaseImplicit)
                ? draft.Base.AvailableCriteria.ExactBase
                : draft.Base.AvailableCriteria.Category;
            return draft with
            {
                Base = draft.Base with
                {
                    ActiveCriterion = activeCriterion,
                },
            };
        };
        fixture.Controller.UpdateCurrentDraft(categoryDraft, ValidationSuccess());

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: true);

        Assert.Equal(
            BaseSearchMode.ExactBase,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);

        fixture.Window.RaiseModifierSelectionChanged(0, isSelected: false);

        Assert.Equal(
            BaseSearchMode.Category,
            fixture.Window.CurrentState?.Draft.Base.ActiveCriterion?.Mode);
    }

    [Fact]
    public async Task PreparePresentationAsync_DelayedProviderMetadataReturnsTheOfficialLabelWithoutSearch()
    {
        var fixture = SearchFixture.Create();
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.PriceCheckService.CategoryLabelLoader = (_, _) => completion.Task;
        var draft = DraftWithBothBaseCriteria("Wand", "Imbued Wand");

        var preparation = fixture.Controller.PreparePresentationAsync(
            draft,
            new PriceCheckerItemPresentation());

        await WaitUntilAsync(() => fixture.PriceCheckService.CategoryLabelLoadCalls.Count == 1);
        Assert.False(preparation.IsCompleted);
        Assert.Empty(fixture.PriceCheckService.Calls);

        completion.SetResult("Wand");
        var presentation = await preparation;

        Assert.Equal("Wand", presentation.CategoryDisplayLabel);
        Assert.Empty(fixture.PriceCheckService.Calls);
    }

    [Fact]
    public async Task PreparePresentationAsync_FailureLeavesTheLabelUnsetAndDoesNotBlockSearch()
    {
        var fixture = SearchFixture.Create();
        fixture.PriceCheckService.CategoryLabelLoader = (_, _) => Task.FromResult<string?>(null);
        var draft = DraftWithBothBaseCriteria();
        var presentation = await fixture.Controller.PreparePresentationAsync(
            draft,
            new PriceCheckerItemPresentation());
        fixture.Controller.UpdateCurrentDraft(draft, ValidationSuccess(), presentation);

        await fixture.Controller.SearchAsync();

        Assert.Null(fixture.Window.CurrentState?.Presentation.CategoryDisplayLabel);
        Assert.Single(fixture.PriceCheckService.Calls);
        Assert.Equal(PriceCheckerSearchViewStatus.ZeroResults, fixture.Window.CurrentSearchState?.Status);
    }

    private static ImmutableArray<TradeSearchItemProperty> AllItemProperties()
    {
        return Enum.GetValues<TradeSearchItemPropertyKind>()
            .Select((kind, index) => ItemProperty(
                kind,
                kind switch
                {
                    TradeSearchItemPropertyKind.TotalDps => 437.45m,
                    TradeSearchItemPropertyKind.PhysicalDps => 169.065m,
                    TradeSearchItemPropertyKind.ElementalDps => 325m,
                    TradeSearchItemPropertyKind.ChaosDps => 42m,
                    TradeSearchItemPropertyKind.AttacksPerSecond => 1.20m,
                    TradeSearchItemPropertyKind.CriticalStrikeChance => 5.00m,
                    _ => index,
                },
                supported: kind != TradeSearchItemPropertyKind.ChaosDps))
            .ToImmutableArray();
    }

    private static TradeSearchItemProperty ItemProperty(
        TradeSearchItemPropertyKind kind,
        decimal minimum,
        bool supported = true)
    {
        return new TradeSearchItemProperty
        {
            Kind = kind,
            Label = kind switch
            {
                TradeSearchItemPropertyKind.TotalDps => "Total DPS",
                TradeSearchItemPropertyKind.PhysicalDps => "Physical DPS",
                TradeSearchItemPropertyKind.ElementalDps => "Elemental DPS",
                TradeSearchItemPropertyKind.ChaosDps => "Chaos DPS",
                TradeSearchItemPropertyKind.AttacksPerSecond => "Attacks per Second",
                TradeSearchItemPropertyKind.CriticalStrikeChance => "Critical Strike Chance",
                _ => kind.ToString(),
            },
            ObservedValue = minimum,
            RequestedMinimum = minimum,
            ProviderResolutionStatus = supported
                ? TradeSearchItemPropertyProviderResolutionStatus.Exact
                : TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
            IsSearchable = supported,
            NotSearchableReason = supported
                ? null
                : "The Path of Exile Trade weapon filter catalog does not expose a Chaos DPS range filter.",
        };
    }

    private static TradeSearchItemPropertyContributionGroup ContributionGroup(
        TradeSearchItemPropertyKind kind,
        params int[] modifierIndexes)
    {
        var target = kind switch
        {
            TradeSearchItemPropertyKind.PhysicalDps => ItemPropertyTarget.PhysicalDamage,
            TradeSearchItemPropertyKind.ElementalDps => ItemPropertyTarget.FireDamage,
            TradeSearchItemPropertyKind.ChaosDps => ItemPropertyTarget.ChaosDamage,
            _ => ItemPropertyTarget.PhysicalDamage,
        };
        return new TradeSearchItemPropertyContributionGroup
        {
            ParentKind = kind,
            Contributions = modifierIndexes
                .Select(index => new TradeSearchItemPropertyContribution
                {
                    ModifierFilterIndex = index,
                    Target = target,
                    Operation = ItemPropertyOperation.Added,
                })
                .ToImmutableArray(),
        };
    }

    private static TradeSearchDraft Draft(
        string name,
        bool selectedModifier = false,
        IReadOnlyList<ResolvedSearchComponent>? modifiers = null)
    {
        return new TradeSearchDraft
        {
            ItemClass = "Body Armours",
            Rarity = "Rare",
            DisplayName = name,
            ParsedBaseType = "Titan Plate",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.titan-plate",
                ResolvedBaseName = "Titan Plate",
            },
            ModifierFilters = modifiers ?? (selectedModifier
                ? [Modifier("+10 to maximum Life", isSelected: true)]
                : []),
        };
    }

    private static TradeSearchDraft DraftWithBothBaseCriteria(
        string categoryName = "One Hand Axes",
        string exactBaseName = "Reaver Axe")
    {
        var category = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = categoryName,
        };
        var exactBase = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = category.Category,
            ExactBaseName = exactBaseName,
        };
        return Draft("Armageddon Thirst") with
        {
            ParsedBaseType = exactBaseName,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = $"base.{exactBaseName.ToLowerInvariant().Replace(' ', '-')}",
                ResolvedBaseName = exactBaseName,
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = category,
                    ExactBase = exactBase,
                },
                ActiveCriterion = category,
            },
        };
    }

    private static ResolvedSearchComponent Modifier(
        string originalText,
        ParsedModifierKind kind = ParsedModifierKind.Prefix,
        bool isSelected = false,
        string? resolvedModifierId = "mod.test",
        bool supportsValueBounds = false,
        decimal? minimum = null,
        decimal? maximum = null,
        string? valueBoundsUnsupportedReason = null)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = originalText,
            CanonicalSignature = originalText,
            ParsedKind = kind,
            ResolutionStatus = resolvedModifierId is null
                ? ModifierCandidateResolutionStatus.Unknown
                : ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = resolvedModifierId,
            ResolvedStatIds = resolvedModifierId is null
                ? []
                : ["stat.test"],
            IsSearchable = resolvedModifierId is not null,
            SupportsValueBounds = supportsValueBounds,
            ValueBoundsUnsupportedReason = valueBoundsUnsupportedReason,
            RequestedMinimum = minimum,
            RequestedMaximum = maximum,
            IsSelected = isSelected,
        };
    }

    private static ResolvedSearchComponent VariantModifier(bool includePresence = false)
    {
        var variants = new List<SearchFilterVariant>
        {
            new()
            {
                Identity = "variant-explicit",
                Label = "Explicit",
                Description = "#% increased Attack Speed (Local)",
                SupportsValueBounds = true,
            },
            new()
            {
                Identity = "variant-crafted",
                Label = "Crafted",
                Description = "#% increased Attack Speed (Local)",
                SupportsValueBounds = true,
            },
            new()
            {
                Identity = "variant-pseudo",
                Label = "Pseudo",
                Description = "+#% total Attack Speed",
                SupportsValueBounds = true,
            },
            new()
            {
                Identity = "variant-implicit",
                Label = "Implicit",
                Description = "#% increased Attack Speed (Local)",
                SupportsValueBounds = true,
            },
            new()
            {
                Identity = "variant-fractured",
                Label = "Fractured",
                Description = "#% increased Attack Speed (Local)",
                SupportsValueBounds = true,
            },
        };
        if (includePresence)
        {
            variants.Add(new SearchFilterVariant
            {
                Identity = "variant-presence",
                Label = "Implicit",
                Description = "increased Attack Speed (Local)",
                SupportsValueBounds = false,
                ValueBoundsUnsupportedReason = "Presence-only filter.",
            });
        }

        return Modifier(
            "20% increased Attack Speed",
            supportsValueBounds: true,
            minimum: 20m) with
        {
            FilterVariants = variants,
            SelectedFilterVariantIdentity = "variant-crafted",
        };
    }

    private static ResolvedSearchComponent ContributorModifier(decimal parentMinimum = 146m)
    {
        var parentVariant = new SearchFilterVariant
        {
            Identity = "variant-parent-pseudo",
            Label = "Pseudo",
            Description = "#% increased total Physical Damage",
            ProviderKind = "pseudo",
            SupportsContributorComposition = true,
            SupportsValueBounds = true,
        };
        var fracturedParentVariant = new SearchFilterVariant
        {
            Identity = "variant-parent-fractured",
            Label = "Fractured",
            Description = "#% increased Physical Damage",
            ProviderKind = "fractured",
            SupportsValueBounds = true,
        };
        var explicitSource = ContributorSource(
            "modifier:0:0",
            "30% increased Physical Damage",
            30m,
            "Explicit");
        var craftedSource = ContributorSource(
            "modifier:1:0",
            "116% increased Physical Damage",
            116m,
            "Crafted");
        return Modifier(
            "146% increased Physical Damage",
            supportsValueBounds: true,
            minimum: parentMinimum) with
        {
            CanonicalSignature = "<number>% increased Physical Damage",
            ValueBoundShape = ModifierBoundShape.Scalar,
            CanonicalNumericValues = [146m],
            IsSelected = false,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = "pseudo.total-physical",
            ProviderStatText = parentVariant.Description,
            FilterVariants = [parentVariant, fracturedParentVariant],
            SelectedFilterVariantIdentity = parentVariant.Identity,
            Sources = [explicitSource, craftedSource],
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Contributors =
            [
                Contributor(
                    "contributor-explicit",
                    explicitSource,
                    30m,
                    "explicit.physical"),
                Contributor(
                    "contributor-crafted",
                    craftedSource,
                    116m,
                    "crafted.physical"),
            ],
        };
    }

    private static SearchComponentContributor Contributor(
        string id,
        SearchComponentSourceProvenance source,
        decimal minimum,
        string providerStatId)
    {
        return new SearchComponentContributor
        {
            ContributorId = id,
            Source = source,
            DisplayText = source.OriginalText,
            RequestedMinimum = minimum,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderIdentity = PathOfExileTradeProviderIdentity.Create(providerStatId),
        };
    }

    private static SearchComponentSourceProvenance ContributorSource(
        string componentId,
        string text,
        decimal value,
        string providerDomain)
    {
        return new SearchComponentSourceProvenance
        {
            ComponentId = componentId,
            OriginalText = text,
            CanonicalSignature = "<number>% increased Physical Damage",
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Local,
            ProviderDomain = providerDomain,
            IsCrafted = providerDomain == "Crafted",
            ResolvedModifierId = $"{providerDomain.ToLowerInvariant()}.physical",
            ResolvedStatIds = ["local_physical_damage_+%"],
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            ValueBoundShape = ModifierBoundShape.Scalar,
            TranslationHandlers = [[]],
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderIdentity = PathOfExileTradeProviderIdentity.Create(
                $"{providerDomain.ToLowerInvariant()}.physical"),
        };
    }

    private static TradeSearchValidationResult ValidationSuccess()
    {
        return TradeSearchValidationResult.FromDiagnostics([]);
    }

    private static PathOfExileTradePriceCheckResult SuccessResult(
        IReadOnlyList<PathOfExileTradeFetchedOffer> offers,
        int total,
        bool? inexact = null,
        IReadOnlyList<string>? resultIds = null,
        IReadOnlyList<string>? fetchedResultIds = null)
    {
        return new PathOfExileTradePriceCheckResult
        {
            IsSuccess = true,
            Stage = PathOfExileTradePriceCheckStage.Completed,
            SearchQueryId = "query-1",
            ResultIds = resultIds ?? [],
            FetchedResultIds = fetchedResultIds ?? [],
            ProviderTotal = total,
            Inexact = inexact,
            Offers = offers,
        };
    }

    private static PathOfExileTradePriceCheckResult FailureResult(
        string message,
        string sourceCode = PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
        string? providerCode = null)
    {
        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Search,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SearchFailed,
                    message,
                    PathOfExileTradePriceCheckStage.Search,
                    sourceCode,
                    ProviderCode: providerCode),
            ],
        };
    }

    private static PathOfExileTradeFetchedOffer Offer(
        string id,
        decimal? amount = 1m,
        string? currency = "chaos",
        string? lastCharacterName = "Seller",
        string? accountName = "Account",
        string? onlineStatus = null,
        string? onlineLeague = null,
        string? rawIndexed = null,
        string? itemName = "Armoured Shell",
        string? typeLine = "Titan Plate",
        string? baseType = null,
        int? itemLevel = 85,
        DateTimeOffset? indexed = null)
    {
        return new PathOfExileTradeFetchedOffer
        {
            Id = id,
            Item = new PathOfExileTradeFetchedItem
            {
                Name = itemName,
                TypeLine = typeLine,
                BaseType = baseType,
                ItemLevel = itemLevel,
            },
            Listing = new PathOfExileTradeListing
            {
                RawIndexed = rawIndexed,
                Indexed = indexed,
                Account = accountName is null && lastCharacterName is null
                    ? null
                    : new PathOfExileTradeListingAccount
                    {
                        Name = accountName,
                        LastCharacterName = lastCharacterName,
                        Online = onlineStatus is null && onlineLeague is null
                            ? null
                            : new PathOfExileTradeListingOnlineState
                            {
                                Status = onlineStatus,
                                League = onlineLeague,
                            },
                    },
                Price = amount is null && currency is null
                    ? null
                    : new PathOfExileTradeListingPrice
                    {
                        Amount = amount,
                        Currency = currency,
                    },
            },
        };
    }

    private static IReadOnlyList<PathOfExileTradeFetchedOffer> OffersFor(IEnumerable<string> ids)
    {
        return ids
            .Select(id => Offer(
                id,
                amount: decimal.Parse(id.AsSpan("id-".Length), CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed record PriceCheckCall(
        TradeSearchDraft? Draft,
        TradeSearchValidationResult? ValidationResult,
        string? LeagueIdentifier,
        int InitialFetchResultCount,
        CancellationToken CancellationToken);

    private sealed record LoadMoreCall(
        string? SearchQueryId,
        IReadOnlyList<string?>? ResultIds,
        CancellationToken CancellationToken);

    private sealed record CategoryLabelLoadCall(
        TradeSearchDraft Draft,
        CancellationToken CancellationToken);

    private sealed class SearchFixture
    {
        private SearchFixture(
            FakeWindow window,
            FakePriceCheckService priceCheckService,
            FakeExternalUrlLauncher externalUrlLauncher,
            PriceCheckerSearchController controller)
        {
            Window = window;
            PriceCheckService = priceCheckService;
            ExternalUrlLauncher = externalUrlLauncher;
            Controller = controller;
        }

        public FakeWindow Window { get; }

        public FakePriceCheckService PriceCheckService { get; }

        public FakeExternalUrlLauncher ExternalUrlLauncher { get; }

        public PriceCheckerSearchController Controller { get; }

        public static SearchFixture Create()
        {
            var window = new FakeWindow
            {
                OfferCapacity = 100,
            };
            var priceCheckService = new FakePriceCheckService();
            var externalUrlLauncher = new FakeExternalUrlLauncher();
            var controller = new PriceCheckerSearchController(
                priceCheckService,
                externalUrlLauncher: externalUrlLauncher);
            controller.AttachWindow(window);
            return new SearchFixture(window, priceCheckService, externalUrlLauncher, controller);
        }
    }

    private sealed class FakeExternalUrlLauncher : IExternalUrlLauncher
    {
        public bool ShouldOpen { get; set; } = true;

        public List<Uri> OpenedUris { get; } = [];

        public bool TryOpen(Uri uri)
        {
            if (!ShouldOpen)
            {
                return false;
            }

            OpenedUris.Add(uri);
            return true;
        }
    }

    private sealed class FakePriceCheckService : IPathOfExileTradePriceCheckService
    {
        public List<PriceCheckCall> Calls { get; } = [];

        public List<LoadMoreCall> LoadMoreCalls { get; } = [];

        public Queue<PathOfExileTradePriceCheckResult> PendingLoadMoreResults { get; } = [];

        public PathOfExileTradePriceCheckResult Result { get; set; } =
            SuccessResult([], total: 0);

        public Func<PriceCheckCall, Task<PathOfExileTradePriceCheckResult>>? Handler { get; set; }

        public Func<LoadMoreCall, Task<PathOfExileTradePriceCheckResult>>? LoadMoreHandler { get; set; }

        public Func<TradeSearchDraft, TradeSearchDraft>? EffectiveDraftResolver { get; set; }

        public List<CategoryLabelLoadCall> CategoryLabelLoadCalls { get; } = [];

        public Func<TradeSearchDraft, CancellationToken, Task<string?>>? CategoryLabelLoader { get; set; }

        public TradeSearchDraft ResolveEffectiveDraft(TradeSearchDraft draft)
        {
            return EffectiveDraftResolver?.Invoke(draft) ?? draft;
        }

        public Task<PathOfExileTradeFilterCatalogProviderResult> InitializeFilterCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PathOfExileTradeFilterCatalogProviderResult());
        }

        public Task<string?> LoadCategoryDisplayLabelAsync(
            TradeSearchDraft draft,
            CancellationToken cancellationToken = default)
        {
            CategoryLabelLoadCalls.Add(new CategoryLabelLoadCall(draft, cancellationToken));
            return CategoryLabelLoader is null
                ? Task.FromResult<string?>(null)
                : CategoryLabelLoader(draft, cancellationToken);
        }

        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            return CheckAsync(
                draft,
                validationResult,
                leagueIdentifier,
                PathOfExileTradeEndpointBuilder.MaximumFetchResultIds,
                cancellationToken);
        }

        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            int initialFetchResultCount,
            CancellationToken cancellationToken = default)
        {
            var call = new PriceCheckCall(
                draft,
                validationResult,
                leagueIdentifier,
                initialFetchResultCount,
                cancellationToken);
            Calls.Add(call);
            return Handler is null
                ? Task.FromResult(Result)
                : Handler(call);
        }

        public Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
            string? searchQueryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default)
        {
            var call = new LoadMoreCall(searchQueryId, resultIds, cancellationToken);
            LoadMoreCalls.Add(call);
            if (LoadMoreHandler is not null)
            {
                return LoadMoreHandler(call);
            }

            if (PendingLoadMoreResults.Count == 0)
            {
                throw new InvalidOperationException("No fake Load More result was configured.");
            }

            return Task.FromResult(PendingLoadMoreResults.Dequeue());
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

        public List<IReadOnlyList<PriceCheckerModifierViewModel>> ModifierCollections { get; } = [];

        public int? OfferCapacity { get; set; }

        public PriceCheckerPlacement? GetDisplayedPlacement()
        {
            return CurrentPlacement;
        }

        public void UpdateContent(PriceCheckerWindowState state)
        {
            CurrentState = state;
        }

        public void UpdateSearch(PriceCheckerSearchViewState state)
        {
            CurrentSearchState = state;
            ModifierCollections.Add(state.Modifiers);
            if (OfferCapacity.HasValue)
            {
                OfferCapacityChanged?.Invoke(
                    this,
                    new PriceCheckerOfferCapacityChangedEventArgs(OfferCapacity.Value));
            }
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

        public void SetPinned(bool isPinned)
        {
            IsPinned = isPinned;
            PinStateChanged?.Invoke(this, isPinned);
        }

        public void RaiseSearchRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseLoadMoreRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            LoadMoreRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseTradeRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            TradeRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseOfferCapacityChanged(int capacity)
        {
            OfferCapacity = capacity;
            OfferCapacityChanged?.Invoke(this, new PriceCheckerOfferCapacityChangedEventArgs(capacity));
        }

        public void RaiseItemPropertySelectionChanged(int itemPropertyIndex, bool isSelected)
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            ItemPropertySelectionChanged?.Invoke(
                this,
                new PriceCheckerItemPropertySelectionChangedEventArgs(itemPropertyIndex, isSelected));
        }

        public void RaiseItemPropertyBoundsChanged(
            int itemPropertyIndex,
            string minimumText,
            string maximumText)
        {
            var property = CurrentSearchState?.ItemProperties.FirstOrDefault(candidate =>
                candidate.SourceIndex == itemPropertyIndex);
            if (property is not null)
            {
                property.MinimumText = minimumText;
                property.MaximumText = maximumText;
            }

            ItemPropertyBoundsChanged?.Invoke(
                this,
                new PriceCheckerItemPropertyBoundsChangedEventArgs(
                    itemPropertyIndex,
                    minimumText,
                    maximumText));
        }

        public void RaiseItemPropertyExpansionChanged(int itemPropertyIndex, bool isExpanded)
        {
            ItemPropertyExpansionChanged?.Invoke(
                this,
                new PriceCheckerItemPropertyExpansionChangedEventArgs(itemPropertyIndex, isExpanded));
        }

        public void RaiseModifierSelectionChanged(int modifierIndex, bool isSelected)
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            ModifierSelectionChanged?.Invoke(
                this,
                new PriceCheckerModifierSelectionChangedEventArgs(modifierIndex, isSelected));
        }

        public void RaiseModifierBoundsChanged(int modifierIndex, string minimumText, string maximumText)
        {
            var modifier = FindModifier(modifierIndex);
            if (modifier is not null)
            {
                modifier.MinimumText = minimumText;
                modifier.MaximumText = maximumText;
            }
            ModifierBoundsChanged?.Invoke(
                this,
                new PriceCheckerModifierBoundsChangedEventArgs(modifierIndex, minimumText, maximumText));
        }

        public void RaiseModifierFilterVariantChanged(int modifierIndex, string variantIdentity)
        {
            ModifierFilterVariantChanged?.Invoke(
                this,
                new PriceCheckerModifierFilterVariantChangedEventArgs(modifierIndex, variantIdentity));
        }

        public void RaiseModifierContributorSelectionChanged(
            int modifierIndex,
            int contributorIndex,
            bool isSelected)
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
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
            var contributor = FindModifier(modifierIndex)?.Contributors.ElementAtOrDefault(contributorIndex);
            if (contributor is not null)
            {
                contributor.MinimumText = minimumText;
                contributor.MaximumText = maximumText;
            }
            ModifierBoundsChanged?.Invoke(
                this,
                new PriceCheckerModifierBoundsChangedEventArgs(
                    modifierIndex,
                    minimumText,
                    maximumText,
                    contributorIndex));
        }

        public void RaiseModifierContributorFilterVariantChanged(
            int modifierIndex,
            int contributorIndex,
            string variantIdentity)
        {
            ModifierFilterVariantChanged?.Invoke(
                this,
                new PriceCheckerModifierFilterVariantChangedEventArgs(
                    modifierIndex,
                    variantIdentity,
                    contributorIndex));
        }

        public void RaiseModifierExpansionChanged(int modifierIndex, bool isExpanded)
        {
            ModifierExpansionChanged?.Invoke(
                this,
                new PriceCheckerModifierExpansionChangedEventArgs(modifierIndex, isExpanded));
        }

        public void RaiseBaseCriterionToggleRequested()
        {
            BaseCriterionToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseResetItemRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            ResetItemRequested?.Invoke(this, EventArgs.Empty);
        }

        private PriceCheckerModifierViewModel? FindModifier(int modifierIndex)
        {
            return CurrentSearchState?.Modifiers.FirstOrDefault(modifier =>
                    modifier.SourceIndex == modifierIndex) ??
                CurrentSearchState?.ItemProperties
                    .SelectMany(property => property.Children)
                    .FirstOrDefault(modifier => modifier.SourceIndex == modifierIndex);
        }
    }
#pragma warning restore CS0067
}

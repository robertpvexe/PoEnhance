using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchSelectedCriteriaCounterTests
{
    [Fact]
    public void Count_SharedCriterionSelectedOnce_RegardlessOfPhysicalParents()
    {
        var fire = Component("base_fire_damage_resistance_%", "+40% to Fire Resistance", 40m) with
        {
            ProviderStatId = "pseudo.pseudo_total_fire_resistance",
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
        };
        var cold = Component("base_cold_damage_resistance_%", "+47% to Cold Resistance", 47m) with
        {
            ProviderStatId = "pseudo.pseudo_total_cold_resistance",
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
        };
        var draft = new TradeSearchDraft
        {
            ModifierFilters = [fire, cold],
            SharedCriteria =
            [
                new SharedSearchCriterion
                {
                    CriterionId = SharedSearchCriterion.TotalElementalResistanceId,
                    ProviderStatId = SharedSearchCriterion.TotalElementalResistanceProviderStatId,
                    DisplayText = SharedSearchCriterion.TotalElementalResistanceDisplayText,
                    IsSelected = true,
                    RequestedMinimum = 87m,
                    ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                },
            ],
        };

        Assert.Equal(1, TradeSearchSelectedCriteriaCounter.Count(draft));
    }

    [Fact]
    public void Count_DuplicatePseudoDexterityProviderStat_CountsOnce()
    {
        var standalone = Component("additional_dexterity", "+8 to Dexterity", 8m) with
        {
            IsSelected = true,
            ProviderStatId = "pseudo.pseudo_total_dexterity",
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            RequestedMinimum = 42m,
        };
        var allAttributes = Component("additional_all_attributes", "+12 to all Attributes", 12m) with
        {
            LogicalContributions =
            [
                new SearchComponentLogicalContribution
                {
                    ContributionId = "dex",
                    LogicalStatId = "additional_dexterity",
                    DisplayText = "+12 to Dexterity",
                    CanonicalSignature = "+<number> to Dexterity",
                    PhysicalSourceIdentity = "phys:1:0:logic:additional_dexterity",
                    CanonicalNumericValues = [12m],
                    IsSelected = true,
                    ProviderStatId = "pseudo.pseudo_total_dexterity",
                    ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                    SupportsValueBounds = true,
                    RequestedMinimum = 42m,
                    SelectedFilterVariantIdentity = "variant-dex",
                },
            ],
        };

        Assert.Equal(
            1,
            TradeSearchSelectedCriteriaCounter.Count(new TradeSearchDraft
            {
                ModifierFilters = [standalone, allAttributes],
            }));
    }

    [Fact]
    public void Count_TwoDistinctLogicalChildren_CountsTwo()
    {
        var allElemental = Component(
            "base_resist_all_elements_%",
            "+15% to all Elemental Resistances",
            15m) with
        {
            LogicalContributions =
            [
                Logical("fire", "base_fire_damage_resistance_%", "pseudo.pseudo_total_fire_resistance"),
                Logical("cold", "base_cold_damage_resistance_%", "pseudo.pseudo_total_cold_resistance"),
            ],
        };

        Assert.Equal(
            2,
            TradeSearchSelectedCriteriaCounter.Count(new TradeSearchDraft
            {
                ModifierFilters = [allElemental],
            }));
    }

    private static SearchComponentLogicalContribution Logical(
        string id,
        string logicalStatId,
        string providerStatId) =>
        new()
        {
            ContributionId = id,
            LogicalStatId = logicalStatId,
            DisplayText = $"+15% to {id}",
            CanonicalSignature = $"+<number>% to {id}",
            PhysicalSourceIdentity = $"phys:0:0:logic:{logicalStatId}",
            CanonicalNumericValues = [15m],
            IsSelected = true,
            ProviderStatId = providerStatId,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            SupportsValueBounds = true,
            RequestedMinimum = 15m,
            SelectedFilterVariantIdentity = $"variant-{id}",
        };

    private static ResolvedSearchComponent Component(string statId, string text, decimal value) =>
        new()
        {
            ComponentId = $"modifier:{statId}",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = text,
            CanonicalSignature = "+<number> stub",
            ParsedKind = ParsedModifierKind.Suffix,
            GenerationType = ModifierGenerationType.Suffix,
            Locality = ModifierLocality.Global,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = $"id.{statId}",
            ResolvedStatIds = [statId],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            RequestedMinimum = value,
        };
}

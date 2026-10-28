using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class SearchComponentContributorActivationTests
{
    [Fact]
    public void UncheckedParent_AllowsIndependentSourceFilteringWithoutComposition()
    {
        var parent = MixedParent(isSelected: false, composition: false, childSelected: true);

        Assert.False(SearchComponentContributorActivation.SupportsComposition(parent));
        Assert.True(SearchComponentContributorActivation.AllowsContributorFiltering(parent));
        Assert.True(SearchComponentContributorActivation.IsFilteringActive(parent));
        Assert.Equal(1, SearchComponentContributorActivation.ActiveSelectionCount(parent));
        Assert.Equal(
            SearchComponentContributorInactiveReason.None,
            SearchComponentContributorActivation.GetInactiveReason(parent, parent.Contributors[0]));
    }

    [Fact]
    public void SelectedNonCompositionParent_KeepsContributorRestriction()
    {
        var parent = MixedParent(isSelected: true, composition: false, childSelected: true);

        Assert.False(SearchComponentContributorActivation.SupportsComposition(parent));
        Assert.False(SearchComponentContributorActivation.AllowsContributorFiltering(parent));
        Assert.False(SearchComponentContributorActivation.IsFilteringActive(parent));
        Assert.Equal(0, SearchComponentContributorActivation.ActiveSelectionCount(parent));
        Assert.Equal(
            SearchComponentContributorInactiveReason.ParentModeDoesNotComposeContributors,
            SearchComponentContributorActivation.GetInactiveReason(parent, parent.Contributors[0]));
    }

    [Fact]
    public void SelectedCompositionParent_KeepsAggregateContributorFiltering()
    {
        var parent = MixedParent(isSelected: true, composition: true, childSelected: true);

        Assert.True(SearchComponentContributorActivation.SupportsComposition(parent));
        Assert.True(SearchComponentContributorActivation.AllowsContributorFiltering(parent));
        Assert.True(SearchComponentContributorActivation.IsFilteringActive(parent));
        Assert.Equal(1, SearchComponentContributorActivation.ActiveSelectionCount(parent));
        Assert.Equal(
            SearchComponentContributorInactiveReason.None,
            SearchComponentContributorActivation.GetInactiveReason(parent, parent.Contributors[0]));
    }

    private static ResolvedSearchComponent MixedParent(
        bool isSelected,
        bool composition,
        bool childSelected)
    {
        var identity = composition ? "pseudo-variant" : "explicit-variant";
        return new ResolvedSearchComponent
        {
            ComponentId = "aggregate:aes",
            OriginalText = "132% increased Armour and Energy Shield",
            CanonicalSignature = "<number>% increased Armour and Energy Shield",
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Local,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "aes",
            ResolvedStatIds = ["local_armour_and_energy_shield_+%"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [132m],
            CanonicalNumericValues = [132m],
            RequestedMinimum = 132m,
            IsSelected = isSelected,
            SelectedFilterVariantIdentity = identity,
            FilterVariants =
            [
                new SearchFilterVariant
                {
                    Identity = identity,
                    Label = composition ? "Pseudo" : "Explicit",
                    Description = composition ? "Pseudo aggregate" : "Explicit standalone",
                    ProviderKind = composition ? "pseudo" : "explicit",
                    Mode = composition
                        ? SearchFilterVariantMode.Aggregate
                        : SearchFilterVariantMode.Standalone,
                    SupportsContributorComposition = composition,
                    SupportsValueBounds = true,
                },
            ],
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Contributors =
            [
                new SearchComponentContributor
                {
                    ContributorId = "fractured-aes",
                    DisplayText = "91% increased Armour and Energy Shield",
                    IsSelected = childSelected,
                    RequestedMinimum = 91m,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    DefaultBoundDirection = ModifierBoundDirection.Minimum,
                    ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                    ProviderIdentity = "variant-fractured",
                    Source = new SearchComponentSourceProvenance
                    {
                        ComponentId = "modifier:0:0",
                        OriginalText = "91% increased Armour and Energy Shield",
                        CanonicalSignature = "<number>% increased Armour and Energy Shield",
                        IsFractured = true,
                        ProviderDomain = "Fractured",
                        CanonicalNumericValues = [91m],
                        ObservedNumericValues = [91m],
                    },
                },
            ],
        };
    }
}

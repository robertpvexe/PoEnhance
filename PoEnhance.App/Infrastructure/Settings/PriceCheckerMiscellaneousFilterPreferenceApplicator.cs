using System.Collections.Immutable;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Settings;

/// <summary>
/// Applies Miscellaneous visibility preferences to drafts used for Trade search/query construction.
/// Hidden optional UI state is retained in the live draft and restored after search when needed.
/// </summary>
internal static class PriceCheckerMiscellaneousFilterPreferenceApplicator
{
    public static bool IsHiddenOptional(
        string key,
        IReadOnlyList<PriceCheckerMiscellaneousFilterPreference> preferences)
    {
        if (!PriceCheckerMiscellaneousFilterRegistry.CanToggleVisibility(key))
        {
            return false;
        }

        var preference = preferences.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.Ordinal));
        return preference is not null && !preference.IsVisible;
    }

    public static TradeSearchDraft ForSearch(
        TradeSearchDraft draft,
        IReadOnlyList<PriceCheckerMiscellaneousFilterPreference> preferences)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(preferences);

        var hideFractured = IsHiddenOptional(
            PriceCheckerMiscellaneousFilterRegistry.FracturedItem,
            preferences);
        var hideBasePercentile = IsHiddenOptional(
            PriceCheckerMiscellaneousFilterRegistry.BasePercentile,
            preferences);
        if (!hideFractured && !hideBasePercentile)
        {
            return draft;
        }

        var itemStateCriteria = draft.ItemStateCriteria;
        if (hideFractured &&
            itemStateCriteria.Fractured is TradeTriState.Yes or TradeTriState.No)
        {
            itemStateCriteria = itemStateCriteria.With(
                TradeItemStateKind.Fractured,
                TradeTriState.Any);
        }

        var requestedItemFilters = draft.RequestedItemFilters;
        if (hideBasePercentile && !requestedItemFilters.IsDefaultOrEmpty)
        {
            requestedItemFilters = requestedItemFilters
                .Select(filter =>
                    filter.Kind == TradeSearchRequestedItemFilterKind.BasePercentile && filter.IsActive
                        ? filter with { IsActive = false }
                        : filter)
                .ToImmutableArray();
        }

        return draft with
        {
            ItemStateCriteria = itemStateCriteria,
            RequestedItemFilters = requestedItemFilters,
        };
    }

    public static TradeSearchDraft RestoreHiddenOptionalUiState(
        TradeSearchDraft searchEffectiveDraft,
        TradeSearchDraft uiDraft,
        IReadOnlyList<PriceCheckerMiscellaneousFilterPreference> preferences)
    {
        ArgumentNullException.ThrowIfNull(searchEffectiveDraft);
        ArgumentNullException.ThrowIfNull(uiDraft);
        ArgumentNullException.ThrowIfNull(preferences);

        var hideFractured = IsHiddenOptional(
            PriceCheckerMiscellaneousFilterRegistry.FracturedItem,
            preferences);
        var hideBasePercentile = IsHiddenOptional(
            PriceCheckerMiscellaneousFilterRegistry.BasePercentile,
            preferences);
        if (!hideFractured && !hideBasePercentile)
        {
            return searchEffectiveDraft;
        }

        var itemStateCriteria = searchEffectiveDraft.ItemStateCriteria;
        if (hideFractured)
        {
            itemStateCriteria = itemStateCriteria.With(
                TradeItemStateKind.Fractured,
                uiDraft.ItemStateCriteria.Fractured);
        }

        var requestedItemFilters = searchEffectiveDraft.RequestedItemFilters;
        if (hideBasePercentile &&
            !uiDraft.RequestedItemFilters.IsDefaultOrEmpty &&
            !requestedItemFilters.IsDefaultOrEmpty)
        {
            var uiBasePercentile = uiDraft.RequestedItemFilters
                .FirstOrDefault(filter => filter.Kind == TradeSearchRequestedItemFilterKind.BasePercentile);
            if (uiBasePercentile is not null)
            {
                requestedItemFilters = requestedItemFilters
                    .Select(filter =>
                        filter.Kind == TradeSearchRequestedItemFilterKind.BasePercentile
                            ? uiBasePercentile
                            : filter)
                    .ToImmutableArray();
            }
        }

        return searchEffectiveDraft with
        {
            ItemStateCriteria = itemStateCriteria,
            RequestedItemFilters = requestedItemFilters,
        };
    }
}

using System.Collections.Immutable;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Settings;

/// <summary>
/// Applies Miscellaneous visibility preferences to drafts used for Trade search/query construction.
/// Hidden optional UI state is retained in the live draft and restored after search when needed.
/// </summary>
internal static class PriceCheckerMiscellaneousFilterPreferenceApplicator
{
    private static readonly (string Key, TradeItemStateKind Kind)[] OptionalItemStateGroups =
    [
        (PriceCheckerMiscellaneousFilterRegistry.FracturedItem, TradeItemStateKind.Fractured),
        (PriceCheckerMiscellaneousFilterRegistry.SynthesisedItem, TradeItemStateKind.Synthesised),
        (PriceCheckerMiscellaneousFilterRegistry.Split, TradeItemStateKind.Split),
        (PriceCheckerMiscellaneousFilterRegistry.Crafted, TradeItemStateKind.Crafted),
        (PriceCheckerMiscellaneousFilterRegistry.Veiled, TradeItemStateKind.Veiled),
        (PriceCheckerMiscellaneousFilterRegistry.Foreseeing, TradeItemStateKind.Foreseeing),
        (PriceCheckerMiscellaneousFilterRegistry.CrucibleItem, TradeItemStateKind.Crucible),
        (PriceCheckerMiscellaneousFilterRegistry.Foulborn, TradeItemStateKind.Foulborn),
        (PriceCheckerMiscellaneousFilterRegistry.Vestigial, TradeItemStateKind.Vestigial),
    ];

    private static readonly (string Key, TradeSearchRequestedItemFilterKind Kind)[] OptionalRequestedFilterGroups =
    [
        (PriceCheckerMiscellaneousFilterRegistry.BasePercentile, TradeSearchRequestedItemFilterKind.BasePercentile),
        (PriceCheckerMiscellaneousFilterRegistry.MemoryStrands, TradeSearchRequestedItemFilterKind.MemoryStrands),
        (PriceCheckerMiscellaneousFilterRegistry.Intangibility, TradeSearchRequestedItemFilterKind.Intangibility),
    ];

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

        var hiddenItemStates = OptionalItemStateGroups
            .Where(group => IsHiddenOptional(group.Key, preferences))
            .ToArray();
        var hiddenRequested = OptionalRequestedFilterGroups
            .Where(group => IsHiddenOptional(group.Key, preferences))
            .Select(group => group.Kind)
            .ToHashSet();

        var itemStateCriteria = draft.ItemStateCriteria;
        foreach (var (_, kind) in hiddenItemStates)
        {
            if (itemStateCriteria.Get(kind) is TradeTriState.Yes or TradeTriState.No)
            {
                itemStateCriteria = itemStateCriteria.With(kind, TradeTriState.Any);
            }
        }

        var requestedItemFilters = draft.RequestedItemFilters.IsDefaultOrEmpty
            ? draft.RequestedItemFilters
            : draft.RequestedItemFilters
                .Select(filter =>
                {
                    var normalized = StripUnsupportedMaximum(filter);
                    return hiddenRequested.Contains(normalized.Kind) && normalized.IsActive
                        ? normalized with { IsActive = false }
                        : normalized;
                })
                .ToImmutableArray();

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

        var hiddenItemStates = OptionalItemStateGroups
            .Where(group => IsHiddenOptional(group.Key, preferences))
            .ToArray();
        var hiddenRequested = OptionalRequestedFilterGroups
            .Where(group => IsHiddenOptional(group.Key, preferences))
            .Select(group => group.Kind)
            .ToHashSet();
        if (hiddenItemStates.Length == 0 && hiddenRequested.Count == 0)
        {
            return searchEffectiveDraft;
        }

        var itemStateCriteria = searchEffectiveDraft.ItemStateCriteria;
        foreach (var (_, kind) in hiddenItemStates)
        {
            itemStateCriteria = itemStateCriteria.With(kind, uiDraft.ItemStateCriteria.Get(kind));
        }

        var requestedItemFilters = searchEffectiveDraft.RequestedItemFilters;
        if (hiddenRequested.Count > 0 &&
            !uiDraft.RequestedItemFilters.IsDefaultOrEmpty &&
            !requestedItemFilters.IsDefaultOrEmpty)
        {
            requestedItemFilters = requestedItemFilters
                .Select(filter =>
                {
                    if (!hiddenRequested.Contains(filter.Kind))
                    {
                        return filter;
                    }

                    var uiFilter = uiDraft.RequestedItemFilters
                        .FirstOrDefault(candidate => candidate.Kind == filter.Kind);
                    return StripUnsupportedMaximum(uiFilter ?? filter);
                })
                .ToImmutableArray();
        }

        return searchEffectiveDraft with
        {
            ItemStateCriteria = itemStateCriteria,
            RequestedItemFilters = requestedItemFilters,
        };
    }

    private static TradeSearchRequestedItemFilter StripUnsupportedMaximum(
        TradeSearchRequestedItemFilter filter) =>
        filter.IsMinimumOnly
            ? filter with
            {
                MaximumText = string.Empty,
                RequestedMaximum = null,
            }
            : filter;
}

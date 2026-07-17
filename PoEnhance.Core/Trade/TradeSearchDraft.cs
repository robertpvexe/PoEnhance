using System.Collections.Immutable;

namespace PoEnhance.Core.Trade;

public sealed record TradeSearchDraft
{
    public string? ItemClass { get; init; }

    public string? Rarity { get; init; }

    public string? DisplayName { get; init; }

    public string? ParsedBaseType { get; init; }

    public IReadOnlyList<string> ItemStates { get; init; } = [];

    public bool IsCorrupted { get; init; }

    public TradeSearchBaseDraft Base { get; init; } = new();

    public int? ItemLevel { get; init; }

    public string? SocketText { get; init; }

    public decimal? BaseRollPercentile { get; init; }

    public ImmutableArray<TradeSearchRequestedItemFilter> RequestedItemFilters { get; init; } = [];

    public IReadOnlyList<string> TraditionalInfluences { get; init; } = [];

    public IReadOnlyList<string> EldritchInfluences { get; init; } = [];

    public TradeItemVariantCriteria ItemVariantCriteria { get; init; } = new();

    public ImmutableArray<TradeSearchItemProperty> ItemProperties { get; init; } = [];

    public ImmutableArray<TradeSearchItemPropertyDiagnostic> ItemPropertyDiagnostics { get; init; } = [];

    public IReadOnlyList<ResolvedSearchComponent> ModifierFilters { get; init; } = [];

    public ImmutableArray<TradeSearchItemPropertyContributionGroup> ItemPropertyContributionGroups { get; init; } = [];

    public IReadOnlyList<TradeSearchDraftDiagnostic> ModifierAggregationDiagnostics { get; init; } = [];

    public TradeListingMode ListingMode { get; init; } = TradeListingMode.InstantBuyout;
}

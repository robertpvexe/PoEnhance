using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Trade;

public sealed record TradeSearchBaseDraft
{
    public ItemBaseResolutionStatus? Status { get; init; }

    public string? ResolvedBaseId { get; init; }

    public string? ResolvedBaseName { get; init; }

    public string? Category { get; init; }

    public ObservedBaseIdentity? Observed { get; init; }

    public AvailableBaseSearchCriteria AvailableCriteria { get; init; } = new();

    public BaseSearchCriterion? ActiveCriterion { get; init; }
}

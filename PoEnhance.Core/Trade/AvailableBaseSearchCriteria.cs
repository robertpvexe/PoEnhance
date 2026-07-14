namespace PoEnhance.Core.Trade;

public sealed record AvailableBaseSearchCriteria
{
    public BaseSearchCriterion? Category { get; init; }

    public BaseSearchCriterion? ExactBase { get; init; }
}

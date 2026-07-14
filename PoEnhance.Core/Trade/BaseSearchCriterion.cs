namespace PoEnhance.Core.Trade;

public sealed record BaseSearchCriterion
{
    public BaseSearchMode Mode { get; init; }

    public string? Category { get; init; }

    public string? ExactBaseName { get; init; }
}

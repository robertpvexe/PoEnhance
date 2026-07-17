namespace PoEnhance.Core.Trade;

public sealed record TradeSearchRequestedItemFilter
{
    public required TradeSearchRequestedItemFilterKind Kind { get; init; }

    public required string Label { get; init; }

    public int? ObservedValue { get; init; }

    public required string CurrentText { get; init; }

    public int? RequestedMinimum { get; init; }

    public bool IsActive { get; init; }

    public TradeSearchRequestedItemFilterValidationStatus LocalValidationStatus { get; init; }

    public TradeSearchItemPropertyProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        TradeSearchItemPropertyProviderResolutionStatus.Unresolved;

    public string? DiagnosticReason { get; init; }
}

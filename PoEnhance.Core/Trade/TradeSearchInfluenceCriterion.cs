namespace PoEnhance.Core.Trade;

public sealed record TradeSearchInfluenceCriterion
{
    public required TradeSearchInfluenceKind Kind { get; init; }

    public required TradeSearchInfluenceFamily Family { get; init; }

    public required string ObservedLine { get; init; }

    public required string DisplayLabel { get; init; }

    public bool IsEnabled { get; init; } = true;

    public TradeSearchItemPropertyProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        TradeSearchItemPropertyProviderResolutionStatus.Unresolved;

    public string? DiagnosticReason { get; init; }
}

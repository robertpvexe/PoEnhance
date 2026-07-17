using System.Collections.Immutable;

namespace PoEnhance.Core.Trade;

public sealed record TradeSearchItemPropertyContributionGroup
{
    public required TradeSearchItemPropertyKind ParentKind { get; init; }

    public ImmutableArray<TradeSearchItemPropertyContribution> Contributions { get; init; } = [];
}

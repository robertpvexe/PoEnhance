using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSelectedRequestedItemFilter
{
    public required TradeSearchRequestedItemFilterKind SourceKind { get; init; }

    public required string ProviderGroupId { get; init; }

    public required string ProviderFilterId { get; init; }

    public required int MinimumValue { get; init; }
}

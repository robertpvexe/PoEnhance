using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSelectedRequestedItemFilter
{
    public required TradeSearchRequestedItemFilterKind SourceKind { get; init; }

    public required string ProviderGroupId { get; init; }

    public required string ProviderFilterId { get; init; }

    public int? MinimumValue { get; init; }

    public int? MaximumValue { get; init; }

    public int? Red { get; init; }

    public int? Green { get; init; }

    public int? Blue { get; init; }

    public int? White { get; init; }

    public bool HasConstraint =>
        MinimumValue.HasValue ||
        MaximumValue.HasValue ||
        Red.HasValue ||
        Green.HasValue ||
        Blue.HasValue ||
        White.HasValue;
}

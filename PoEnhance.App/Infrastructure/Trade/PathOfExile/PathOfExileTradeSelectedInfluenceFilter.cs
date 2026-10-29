using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSelectedInfluenceFilter
{
    public required TradeSearchInfluenceKind SourceKind { get; init; }

    public required TradeSearchInfluenceFamily Family { get; init; }

    public string? StatId { get; init; }

    public string? ProviderGroupId { get; init; }

    public string? ProviderFilterId { get; init; }

    public string? Option { get; init; }
}

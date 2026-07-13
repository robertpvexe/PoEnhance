using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Trade;

public sealed record TradeSearchBaseDraft
{
    public ItemBaseResolutionStatus? Status { get; init; }

    public string? ResolvedBaseId { get; init; }

    public string? ResolvedBaseName { get; init; }
}

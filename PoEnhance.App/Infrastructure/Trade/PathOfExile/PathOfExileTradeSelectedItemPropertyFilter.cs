namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSelectedItemPropertyFilter
{
    public required int SourceItemPropertyIndex { get; init; }

    public required string ProviderGroupId { get; init; }

    public required string ProviderFilterId { get; init; }

    public required decimal RequestedMinimum { get; init; }

    public decimal? RequestedMaximum { get; init; }
}

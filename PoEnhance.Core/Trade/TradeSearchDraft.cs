namespace PoEnhance.Core.Trade;

public sealed record TradeSearchDraft
{
    public string? ItemClass { get; init; }

    public string? Rarity { get; init; }

    public string? DisplayName { get; init; }

    public string? ParsedBaseType { get; init; }

    public TradeSearchBaseDraft Base { get; init; } = new();

    public int? ItemLevel { get; init; }

    public IReadOnlyList<string> TraditionalInfluences { get; init; } = [];

    public IReadOnlyList<string> EldritchInfluences { get; init; } = [];

    public IReadOnlyList<TradeModifierFilterDraft> ModifierFilters { get; init; } = [];

    public TradeListingMode ListingMode { get; init; } = TradeListingMode.MerchantOnly;
}

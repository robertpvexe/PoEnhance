using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeRequestedItemFilterMappingCatalog
{
    private readonly IReadOnlyDictionary<TradeSearchRequestedItemFilterKind, PathOfExileTradeRequestedItemFilterMapping> byKind;

    public PathOfExileTradeRequestedItemFilterMappingCatalog(
        string reviewReference,
        IEnumerable<PathOfExileTradeRequestedItemFilterMapping> mappings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewReference);
        ArgumentNullException.ThrowIfNull(mappings);
        ReviewReference = reviewReference;
        Mappings = mappings.ToArray();
        byKind = Mappings.ToDictionary(mapping => mapping.Kind);
    }

    public string ReviewReference { get; }

    public IReadOnlyList<PathOfExileTradeRequestedItemFilterMapping> Mappings { get; }

    public bool TryGet(
        TradeSearchRequestedItemFilterKind kind,
        out PathOfExileTradeRequestedItemFilterMapping mapping) =>
        byKind.TryGetValue(kind, out mapping!);
}

internal sealed record PathOfExileTradeRequestedItemFilterMapping
{
    public required TradeSearchRequestedItemFilterKind Kind { get; init; }

    public required string ProviderGroupId { get; init; }

    public required string ProviderFilterId { get; init; }

    public required string ExpectedOfficialText { get; init; }

    public bool RequiresNumericMinMax { get; init; }

    public int MinimumSupportedValue { get; init; }

    public int MaximumSupportedValue { get; init; }
}

using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemPropertyMappingCatalog
{
    private readonly IReadOnlyDictionary<TradeSearchItemPropertyKind, PathOfExileTradeItemPropertyMapping> byKind;

    public PathOfExileTradeItemPropertyMappingCatalog(
        string reviewReference,
        IEnumerable<PathOfExileTradeItemPropertyMapping> mappings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewReference);
        ArgumentNullException.ThrowIfNull(mappings);

        ReviewReference = reviewReference;
        Mappings = mappings.ToArray();
        byKind = Mappings.ToDictionary(mapping => mapping.Kind);
    }

    public string ReviewReference { get; }

    public IReadOnlyList<PathOfExileTradeItemPropertyMapping> Mappings { get; }

    public bool TryGet(
        TradeSearchItemPropertyKind kind,
        out PathOfExileTradeItemPropertyMapping mapping)
    {
        return byKind.TryGetValue(kind, out mapping!);
    }
}

internal sealed record PathOfExileTradeItemPropertyMapping
{
    public required TradeSearchItemPropertyKind Kind { get; init; }

    public bool IsSupported { get; init; }

    public string? ProviderGroupId { get; init; }

    public string? ProviderFilterId { get; init; }

    public string? ExpectedOfficialText { get; init; }

    public string? ExpectedOfficialTip { get; init; }

    public bool RequiresExactOfficialTextMatch { get; init; } = true;

    public bool RequiresNumericMinMax { get; init; }

    public string? UnsupportedReason { get; init; }
}

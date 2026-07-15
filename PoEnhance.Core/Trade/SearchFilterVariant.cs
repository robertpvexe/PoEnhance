namespace PoEnhance.Core.Trade;

public sealed record SearchFilterVariant
{
    public required string Identity { get; init; }

    public required string Label { get; init; }

    public required string Description { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }
}

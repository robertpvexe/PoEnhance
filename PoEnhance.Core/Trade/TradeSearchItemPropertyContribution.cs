using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

public sealed record TradeSearchItemPropertyContribution
{
    public required int ModifierFilterIndex { get; init; }

    public required ItemPropertyTarget Target { get; init; }

    public required ItemPropertyOperation Operation { get; init; }

    public string? ReviewedSemanticDescriptorId { get; init; }
}

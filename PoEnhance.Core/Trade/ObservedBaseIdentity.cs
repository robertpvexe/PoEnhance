using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Trade;

public sealed record ObservedBaseIdentity
{
    public ItemBaseResolutionStatus? Status { get; init; }

    public string? ExactBaseId { get; init; }

    public string? ExactBaseName { get; init; }

    public string? Category { get; init; }
}

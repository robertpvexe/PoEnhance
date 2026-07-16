namespace PoEnhance.GameData;

public sealed record ItemPropertyContribution
{
    public IReadOnlyList<ItemPropertyTarget> Targets { get; init; } = [];

    public ItemPropertyOperation Operation { get; init; }
}

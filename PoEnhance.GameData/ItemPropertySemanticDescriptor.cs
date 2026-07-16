namespace PoEnhance.GameData;

public sealed record ItemPropertySemanticDescriptor
{
    public string? Id { get; init; }

    public IReadOnlyList<string> OrderedStatIds { get; init; } = [];

    public IReadOnlyList<ItemPropertyContribution> Contributions { get; init; } = [];

    public ItemPropertyApplicability Applicability { get; init; } = ItemPropertyApplicability.Unknown;

    public IReadOnlyList<ItemPropertySemanticEvidence> Evidence { get; init; } = [];
}

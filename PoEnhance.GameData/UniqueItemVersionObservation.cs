namespace PoEnhance.GameData;

public sealed record UniqueItemVersionObservation
{
    public string? Id { get; init; }

    public string? Label { get; init; }

    public UniqueItemVersionRole Role { get; init; }

    public string? BaseType { get; init; }

    public IReadOnlyList<UniqueModifierBlock> ModifierBlocks { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}

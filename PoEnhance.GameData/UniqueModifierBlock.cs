namespace PoEnhance.GameData;

public sealed record UniqueModifierBlock
{
    public string? Id { get; init; }

    public UniqueModifierBlockKind Kind { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public IReadOnlyList<string> CanonicalSignatures { get; init; } = [];

    public UniqueModifierMechanicalMapping MechanicalMapping { get; init; } = new();

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}

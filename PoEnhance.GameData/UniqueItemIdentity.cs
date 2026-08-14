namespace PoEnhance.GameData;

public sealed record UniqueItemIdentity
{
    public string? Id { get; init; }

    public string? CanonicalName { get; init; }

    public string? CanonicalIdentityKey { get; init; }

    public UniqueItemKind Kind { get; init; }

    public IReadOnlyList<string> BaseTypeEvidence { get; init; } = [];

    public IReadOnlyList<UniqueItemVersionObservation> Versions { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}

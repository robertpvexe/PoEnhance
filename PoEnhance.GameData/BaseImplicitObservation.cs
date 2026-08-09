namespace PoEnhance.GameData;

/// <summary>
/// The ordered implicit set observed for one canonical base at one exact source snapshot.
/// </summary>
public sealed record BaseImplicitObservation
{
    public string? CanonicalBaseId { get; init; }

    public string? SourceSnapshotId { get; init; }

    public IReadOnlyList<string> ImplicitModifierIds { get; init; } = [];

    public IReadOnlyList<string?> MechanicalEffectIds { get; init; } = [];

    public string? ImplicitSetMechanicalSignature { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

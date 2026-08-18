namespace PoEnhance.GameData;

public sealed record UniqueModifierBlock
{
    public string? Id { get; init; }

    public UniqueModifierBlockKind Kind { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public IReadOnlyList<string> CanonicalSignatures { get; init; } = [];

    public UniqueModifierSourceSemantics SourceSemantics { get; init; }

    /// <summary>
    /// Stable, source-derived variant memberships for a generated candidate. Empty for fixed
    /// blocks. These ids prove catalog-pool membership without turning display names into
    /// production branching keys.
    /// </summary>
    public IReadOnlyList<string> CandidatePoolMembershipIds { get; init; } = [];

    public UniqueModifierMechanicalMapping MechanicalMapping { get; init; } = new();

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}

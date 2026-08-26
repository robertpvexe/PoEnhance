using System.Text.Json.Serialization;

namespace PoEnhance.GameData;

public sealed record UniqueModifierBlock
{
    public string? Id { get; init; }

    public UniqueModifierBlockKind Kind { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public IReadOnlyList<string> CanonicalSignatures { get; init; } = [];

    public UniqueModifierSourceSemantics SourceSemantics { get; init; }

    /// <summary>
    /// Source-derived semantics used only as additional mechanical-candidate evidence. Empty
    /// dimensions retain the legacy text/value matching behavior.
    /// </summary>
    public UniqueModifierSemanticFingerprint SourceSemanticFingerprint { get; init; } = new();

    /// <summary>
    /// Stable, source-derived variant memberships for a generated candidate. Empty for fixed
    /// blocks. These ids prove catalog-pool membership without turning display names into
    /// production branching keys.
    /// </summary>
    public IReadOnlyList<string> CandidatePoolMembershipIds { get; init; } = [];

    /// <summary>
    /// Source-derived memberships in independently selectable option choices. These remain
    /// separate from generated-candidate semantics and from atomic item versions.
    /// </summary>
    public IReadOnlyList<UniqueModifierOptionChoiceMembership> OptionChoiceMemberships { get; init; } = [];

    /// <summary>
    /// Present only when pinned source mechanics prove that this block can be displayed as
    /// independently bounded line components without changing its complete stat vector.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UniqueModifierComposition? Composition { get; init; }

    public UniqueModifierMechanicalMapping MechanicalMapping { get; init; } = new();

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}

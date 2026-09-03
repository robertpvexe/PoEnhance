using System.Text.Json.Serialization;

namespace PoEnhance.GameData;

public sealed record UniqueModifierMechanicalMapping
{
    public UniqueModifierMechanicalMappingStatus Status { get; init; }

    public IReadOnlyList<string> ModifierIds { get; init; } = [];

    public IReadOnlyList<string> StatIds { get; init; } = [];

    /// <summary>
    /// Structured evidence retained for mechanics that required a non-trivial, source-proven
    /// resolution. Null means the ordinary exact-vector path was sufficient.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UniqueModifierMechanicalProvenance? Provenance { get; init; }

    /// <summary>
    /// Compact ExactConflict candidate provenance and structural subtype. Present only when
    /// status is Ambiguous with ExactConflict evidence. Never authorizes selection or search.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UniqueMechanicalConflictEvidence? ConflictEvidence { get; init; }

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }
}

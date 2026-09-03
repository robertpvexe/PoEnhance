using System.Text.Json.Serialization;

namespace PoEnhance.GameData;

/// <summary>
/// Compact per-candidate ExactConflict provenance. Retains IDs and normalized semantic evidence
/// without copying full modifier, translation, or Trade catalog objects.
/// </summary>
public sealed record UniqueMechanicalConflictCandidate
{
    public string ModifierId { get; init; } = string.Empty;

    public IReadOnlyList<string> StatIds { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Domain { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceGenerationType { get; init; }

    public ModifierSourceAvailability SourceAvailability { get; init; }

    public UniqueModifierSemanticLocality Locality { get; init; }

    public IReadOnlyList<string> TranslationIds { get; init; } = [];

    public IReadOnlyList<string> ValueFormats { get; init; } = [];

    public IReadOnlyList<string> Handlers { get; init; } = [];

    /// <summary>
    /// Deterministic structural markers derived from mod/stat ids and handlers
    /// (for example <c>permyriad</c>, <c>deprecated-name</c>, <c>handler-negate</c>).
    /// </summary>
    public IReadOnlyList<string> EncodingMarkers { get; init; } = [];
}

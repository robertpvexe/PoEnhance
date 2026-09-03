using System.Text.Json.Serialization;

namespace PoEnhance.GameData;

/// <summary>
/// ExactConflict diagnostic provenance retained on Ambiguous Unique mechanical mappings.
/// Never authorizes selection, Trade alternatives, or searchable resolution.
/// </summary>
public sealed record UniqueMechanicalConflictEvidence
{
    public UniqueMechanicalConflictKind Kind { get; init; }

    public IReadOnlyList<UniqueMechanicalConflictCandidate> Candidates { get; init; } = [];

    [JsonIgnore]
    public int CandidateCount => Candidates.Count;
}

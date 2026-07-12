using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed record ModifierCandidateResolutionResult(
    int ParsedModifierIndex,
    ParsedModifier ParsedModifier,
    string? ParsedModifierName,
    ParsedModifierKind ParsedModifierKind,
    ModifierGenerationType? GenerationType,
    ModifierCandidateResolutionStatus Status,
    IReadOnlyList<ModifierDefinition> Candidates,
    IReadOnlyList<ModifierCandidateResolutionDiagnostic> Diagnostics,
    int NameCandidateCount = 0,
    int GenerationKindCandidateCount = 0,
    int EligibilityCandidateCount = 0,
    int ExcludedCandidateCount = 0,
    IReadOnlyList<ModifierDefinition>? ExcludedCandidates = null)
{
    public int CandidateCount => Candidates.Count;
}

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
    IReadOnlyList<ModifierCandidateResolutionDiagnostic> Diagnostics)
{
    public int CandidateCount => Candidates.Count;
}

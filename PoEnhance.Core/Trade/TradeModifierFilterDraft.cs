using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

public sealed record TradeModifierFilterDraft
{
    public string OriginalText { get; init; } = string.Empty;

    public ParsedModifierKind ParsedKind { get; init; }

    public ModifierGenerationType? GenerationType { get; init; }

    public string? ParsedModifierName { get; init; }

    public string? CategoryText { get; init; }

    public bool IsCrafted { get; init; }

    public bool IsFractured { get; init; }

    public bool IsVeiled { get; init; }

    public ModifierCandidateResolutionStatus? ResolutionStatus { get; init; }

    public string? ResolvedModifierId { get; init; }

    public string? ResolvedModifierName { get; init; }

    public bool IsSelected { get; init; }
}

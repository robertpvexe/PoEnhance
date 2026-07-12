using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed record ModifierCandidateResolutionsDisplay
{
    public bool IsAvailable { get; init; }

    public string Diagnostic { get; init; } = "Game data not loaded";

    public IReadOnlyList<ModifierCandidateResolutionItemDisplay> Results { get; init; } = [];
}

internal sealed record ModifierCandidateResolutionItemDisplay
{
    public int ParsedModifierIndex { get; init; }

    public ParsedModifier ParsedModifier { get; init; } = default!;

    public string Status { get; init; } = "Unknown";

    public string Diagnostic { get; init; } = "Not detected";

    public int CandidateCount { get; init; }

    public IReadOnlyList<string> CandidateLabels { get; init; } = [];
}

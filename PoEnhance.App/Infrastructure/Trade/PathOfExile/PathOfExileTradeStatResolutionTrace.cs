using PoEnhance.Core.Items.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatResolutionTrace
{
    public string CopiedNormalizedTemplate { get; init; } = string.Empty;

    public string? ResolvedModifierId { get; init; }

    public IReadOnlyList<string> InternalStatIds { get; init; } = [];

    public ModifierLocality ExpectedLocality { get; init; } = ModifierLocality.Unknown;

    public string? ProviderCandidateGroupKey { get; init; }

    public IReadOnlyList<PathOfExileTradeStatMatchCandidate> CompatibleProviderCandidates { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeStatCandidateRejection> Rejections { get; init; } = [];

    public string? SelectedProviderStatId { get; init; }

    public string? FinalDiagnosticCode { get; init; }
}

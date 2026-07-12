namespace PoEnhance.App.Infrastructure.GameData;

using PoEnhance.Core.Items.GameData;

internal sealed record ItemBaseResolutionDisplay
{
    public bool IsAvailable { get; init; }

    public string Status { get; init; } = "Unavailable";

    public string ResolvedBaseName { get; init; } = "Not detected";

    public string ResolvedBaseId { get; init; } = "Not detected";

    public string Diagnostic { get; init; } = "Game data not loaded";

    public int CandidateCount { get; init; }

    public IReadOnlyList<string> CandidateNames { get; init; } = [];

    public ItemBaseResolutionResult? Result { get; init; }
}

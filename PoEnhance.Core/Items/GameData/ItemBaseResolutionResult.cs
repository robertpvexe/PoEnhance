using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed record ItemBaseResolutionResult
{
    public CanonicalItemClassIdentity? ItemClassIdentity { get; init; }

    public ItemBaseResolutionStatus Status { get; init; }

    public ItemBaseRecord? MatchedItemBase { get; init; }

    public string? ResolvedBaseId { get; init; }

    public string? ResolvedBaseName { get; init; }

    public IReadOnlyList<ItemBaseRecord> Candidates { get; init; } = [];

    public IReadOnlyList<ItemBaseResolutionDiagnostic> Diagnostics { get; init; } = [];
}

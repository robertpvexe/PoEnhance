namespace PoEnhance.GameData;

/// <summary>
/// Source-backed identity for a passive-tree skill. The hash is the authoritative
/// mechanical parameter used by passive-hash translations; it is not a modifier id.
/// </summary>
public sealed record PassiveSkillIdentity
{
    public required string CanonicalName { get; init; }

    public required int PassiveHash { get; init; }

    public string? InternalId { get; init; }

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}

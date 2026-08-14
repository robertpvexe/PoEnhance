namespace PoEnhance.GameData;

public sealed record UniqueCatalogSourceObservation
{
    public string? Id { get; init; }

    public string? ManifestSourceId { get; init; }

    public string? RepositoryUri { get; init; }

    public string? Tag { get; init; }

    public string? CommitSha { get; init; }

    public string? SourcePath { get; init; }

    public bool IsGenerated { get; init; }

    public UniqueItemKind ObservedKind { get; init; }

    public string? RawEntrySha256 { get; init; }

    public string? ObservedName { get; init; }

    public IReadOnlyList<string> ObservedBaseTypes { get; init; } = [];

    public string? CanonicalIdentityKey { get; init; }

    public string? IdentityNormalizationRule { get; init; }

    public string? IdentityDecisionReason { get; init; }
}

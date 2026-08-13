namespace PoEnhance.GameData;

public sealed record UniqueFoulbornRelationshipSourceObservation
{
    public string? Id { get; init; }

    public string? ManifestSourceId { get; init; }

    public string? RepositoryUri { get; init; }

    public string? Tag { get; init; }

    public string? CommitSha { get; init; }

    public string? SourcePath { get; init; }

    public string? SourceFileSha256 { get; init; }
}

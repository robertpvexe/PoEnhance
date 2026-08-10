namespace PoEnhance.GameData;

public sealed record StatTranslationSourceSnapshot
{
    public string? Id { get; init; }

    public StatTranslationSnapshotRole Role { get; init; }

    public string? ManifestSourceId { get; init; }

    public string? RepositoryUri { get; init; }

    public string? CommitSha { get; init; }

    public string? DataVersion { get; init; }

    public IReadOnlyList<StatTranslationSourceFile> Files { get; init; } = [];
}

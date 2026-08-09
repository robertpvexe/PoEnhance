namespace PoEnhance.GameData;

public sealed record BaseImplicitSourceSnapshot
{
    public string? Id { get; init; }

    public BaseImplicitSnapshotRole Role { get; init; }

    public string? ManifestSourceId { get; init; }

    public string? RepositoryUri { get; init; }

    public string? CommitSha { get; init; }

    public string? DataVersion { get; init; }

    public IReadOnlyList<BaseImplicitSourceFile> Files { get; init; } = [];
}

namespace PoEnhance.DataImport;

public sealed record RePoeSourceSnapshotManifest
{
    public int SnapshotVersion { get; init; } = 2;

    public string? RepositoryUri { get; init; }

    public string? Branch { get; init; }

    public string? CommitSha { get; init; }

    public string? PackageDataVersion { get; init; }

    public DateTimeOffset BuildTimestampUtc { get; init; }

    public IReadOnlyList<RePoeSourceSnapshotFile> Files { get; init; } = [];
}

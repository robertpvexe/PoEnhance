namespace PoEnhance.DataImport;

public sealed record RePoeSourceSnapshotFile
{
    public string? LogicalInputRole { get; init; }

    public string? SnapshotRole { get; init; }

    public string? RepositoryUri { get; init; }

    public string? Branch { get; init; }

    public string? CommitSha { get; init; }

    public string? SourceDataVersion { get; init; }

    public string? OriginalResolvedPath { get; init; }

    public string? RetainedFileName { get; init; }

    public string? RetainedRelativePath { get; init; }

    public long SizeBytes { get; init; }

    public string? Sha256 { get; init; }
}

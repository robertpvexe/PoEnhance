namespace PoEnhance.DataImport;

internal sealed record RePoeSourceSnapshotInput
{
    public required string LogicalInputRole { get; init; }

    public required string PackageInputLabel { get; init; }

    public required string OriginalPath { get; init; }

    public long ExpectedSizeBytes { get; init; }

    public required string ExpectedSha256 { get; init; }

    public string SnapshotRole { get; init; } = "current";

    public string RepositoryUri { get; init; } = "unknown";

    public string Branch { get; init; } = "unknown";

    public string CommitSha { get; init; } = "unknown";

    public string SourceDataVersion { get; init; } = "unknown";
}

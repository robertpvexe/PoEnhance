namespace PoEnhance.DataImport;

public sealed record RePoeSourceSnapshotFile
{
    public string? LogicalInputRole { get; init; }

    public string? OriginalResolvedPath { get; init; }

    public string? RetainedFileName { get; init; }

    public long SizeBytes { get; init; }

    public string? Sha256 { get; init; }
}

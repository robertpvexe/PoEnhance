namespace PoEnhance.GameData;

public sealed record GameDataPackageSource
{
    public string? SourceId { get; init; }

    public DateTimeOffset RetrievedAtUtc { get; init; }

    public string? SourceVersion { get; init; }

    public string? SourceUri { get; init; }

    public string? SourceBranch { get; init; }

    public string? SourceRoot { get; init; }

    public string? SourceDataRoot { get; init; }

    public IReadOnlyList<GameDataPackageInputFileFingerprint> InputFiles { get; init; } = [];
}

namespace PoEnhance.GameData;

public sealed record GameDataPackageSource
{
    public string? SourceId { get; init; }

    public DateTimeOffset RetrievedAtUtc { get; init; }

    public string? SourceVersion { get; init; }

    public string? SourceUri { get; init; }
}

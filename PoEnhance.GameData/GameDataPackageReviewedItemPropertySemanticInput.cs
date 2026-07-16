namespace PoEnhance.GameData;

public sealed record GameDataPackageReviewedItemPropertySemanticInput
{
    public string? SourceId { get; init; }

    public string? Label { get; init; }

    public string? DisplayPath { get; init; }

    public long SizeBytes { get; init; }

    public string? Sha256 { get; init; }

    public int SchemaVersion { get; init; }

    public string? ReviewVersion { get; init; }
}

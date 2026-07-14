namespace PoEnhance.GameData;

public sealed record GameDataPackageInputFileFingerprint
{
    public string? Label { get; init; }

    public string? RelativePath { get; init; }

    public long SizeBytes { get; init; }

    public string? Sha256 { get; init; }
}

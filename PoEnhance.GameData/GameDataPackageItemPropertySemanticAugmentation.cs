namespace PoEnhance.GameData;

public sealed record GameDataPackageItemPropertySemanticAugmentation
{
    public string? OperationId { get; init; }

    public string? InputPackageLabel { get; init; }

    public string? InputPackageDisplayPath { get; init; }

    public long InputPackageSizeBytes { get; init; }

    public string? InputPackageSha256 { get; init; }

    public string? InputPackageDataVersion { get; init; }
}

namespace PoEnhance.DataImport;

public sealed record GameDataPackageWeaponPropertyAugmentationRequest
{
    public required string InputPackagePath { get; init; }

    public required string BaseItemsPath { get; init; }

    public required string OutputPath { get; init; }

    public required string DataVersion { get; init; }
}

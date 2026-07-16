namespace PoEnhance.DataImport;

public sealed record GameDataPackageSemanticAugmentationRequest
{
    public string? InputPackagePath { get; init; }

    public string? ItemPropertySemanticsPath { get; init; }

    public string? OutputPath { get; init; }

    public string? DataVersion { get; init; }
}

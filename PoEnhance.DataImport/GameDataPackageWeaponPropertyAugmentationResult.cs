using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed record GameDataPackageWeaponPropertyAugmentationResult
{
    public bool IsSuccess { get; init; }

    public string? InputPackageSha256 { get; init; }

    public long InputPackageSizeBytes { get; init; }

    public string? BaseItemsSha256 { get; init; }

    public long BaseItemsSizeBytes { get; init; }

    public string? OutputSha256 { get; init; }

    public long OutputSizeBytes { get; init; }

    public int ItemBaseCount { get; init; }

    public int ItemBasesWithWeaponProperties { get; init; }

    public int ItemBasesWithCompletePhysicalRange { get; init; }

    public int ItemBasesWithAttackTime { get; init; }

    public int ItemBasesWithCriticalStrikeChance { get; init; }

    public IReadOnlyDictionary<string, int> MissingCompleteWeaponPropertiesByClass { get; init; } =
        new Dictionary<string, int>();

    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];

    public GameDataPackage? Package { get; init; }
}

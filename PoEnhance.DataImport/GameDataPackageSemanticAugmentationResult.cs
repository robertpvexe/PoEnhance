using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed record GameDataPackageSemanticAugmentationResult
{
    public GameDataPackageSemanticAugmentationExitCode ExitCode { get; init; }

    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];

    public GameDataPackageBuildRecordCounts FinalCounts { get; init; } = new();

    public string? InputPackagePath { get; init; }

    public long? InputPackageSizeBytes { get; init; }

    public string? InputPackageSha256 { get; init; }

    public string? OutputPath { get; init; }

    public long? OutputFileSizeBytes { get; init; }

    public string? Sha256 { get; init; }

    public GameDataPackage? Package { get; init; }

    public bool IsSuccess => ExitCode == GameDataPackageSemanticAugmentationExitCode.Success;
}

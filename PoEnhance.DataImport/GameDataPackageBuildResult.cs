using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed record GameDataPackageBuildResult
{
    public GameDataPackageBuildExitCode ExitCode { get; init; }

    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<GameDataPackageBuildSourceSummary> SourceSummaries { get; init; } = [];

    public GameDataPackageBuildRecordCounts FinalCounts { get; init; } = new();

    public string? OutputPath { get; init; }

    public long? OutputFileSizeBytes { get; init; }

    public string? Sha256 { get; init; }

    public GameDataPackage? Package { get; init; }

    public bool IsSuccess => ExitCode == GameDataPackageBuildExitCode.Success;
}

namespace PoEnhance.GameData;

public sealed record GameDataPackageLoadResult
{
    public bool IsSuccess => Package is not null && Diagnostics.Count == 0 && ValidationErrors.Count == 0;

    public GameDataPackage? Package { get; init; }

    public IReadOnlyList<GameDataPackageLoadDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<GameDataValidationError> ValidationErrors { get; init; } = [];

    public string? SourcePath { get; init; }
}

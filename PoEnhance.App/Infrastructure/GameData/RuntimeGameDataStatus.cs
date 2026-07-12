using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed record RuntimeGameDataStatus
{
    public RuntimeGameDataState State { get; init; } = RuntimeGameDataState.NotConfigured;

    public string? PackagePath { get; init; }

    public GameDataPackagePathSource PathSource { get; init; } = GameDataPackagePathSource.None;

    public string? DataVersion { get; init; }

    public string? SourceVersion { get; init; }

    public int ItemBaseCount { get; init; }

    public int ModifierCount { get; init; }

    public int StatCount { get; init; }

    public int StatTranslationCount { get; init; }

    public IReadOnlyList<GameDataPackageLoadDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<GameDataValidationError> ValidationErrors { get; init; } = [];

    public string? FailureMessage { get; init; }

    public GameDataCatalog? Catalog { get; init; }

    public GameDataPackage? Package { get; init; }
}

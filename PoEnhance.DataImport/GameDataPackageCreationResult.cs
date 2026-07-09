using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed record GameDataPackageCreationResult
{
    public GameDataPackage? Package { get; init; }

    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];

    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error);
}

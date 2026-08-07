using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed record RePoeModsByBaseImportResult
{
    public BaseModifierSourceEvidence? Evidence { get; init; }

    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];

    public RePoeModsByBaseImportAudit Audit { get; init; } = new();

    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error);
}

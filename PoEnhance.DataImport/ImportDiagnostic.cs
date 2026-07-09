namespace PoEnhance.DataImport;

public sealed record ImportDiagnostic(
    string Code,
    ImportDiagnosticSeverity Severity,
    string? SourceRecordId,
    string Message);

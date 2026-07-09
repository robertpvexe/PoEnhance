namespace PoEnhance.DataImport;

public sealed record ImportResult<TRecord>
{
    public IReadOnlyList<TRecord> ImportedRecords { get; init; } = [];

    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];

    public int SourceRecordsRead { get; init; }

    public int RecordsImported { get; init; }

    public int RecordsSkipped { get; init; }

    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error);
}

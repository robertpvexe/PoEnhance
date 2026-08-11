using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed record PoBUniqueCatalogImportResult
{
    public UniqueItemCatalog? Catalog { get; init; }

    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];

    public int SourceRecordsRead { get; init; }

    public int RecordsImported { get; init; }

    public int RecordsSkipped { get; init; }
}

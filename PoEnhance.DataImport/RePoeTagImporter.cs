using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeTagImporter
{
    public ImportResult<TagDefinition> Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ImportResult<TagDefinition>
            {
                Diagnostics = [new ImportDiagnostic(
                    RePoeImportDiagnosticCodes.FileNotFound,
                    ImportDiagnosticSeverity.Error,
                    null,
                    $"RePoE tags.json file was not found: {filePath}")],
            };
        }

        using var stream = File.OpenRead(filePath);
        return Import(stream);
    }

    public ImportResult<TagDefinition> Import(Stream stream)
    {
        try
        {
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Failure(RePoeImportDiagnosticCodes.SchemaUnsupported,
                    "RePoE tags.json root must be an array of tag ids.");
            }

            var records = new List<TagDefinition>();
            var diagnostics = new List<ImportDiagnostic>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var read = 0;
            var skipped = 0;
            foreach (var sourceRecord in document.RootElement.EnumerateArray())
            {
                read++;
                if (sourceRecord.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(sourceRecord.GetString()))
                {
                    skipped++;
                    diagnostics.Add(new ImportDiagnostic(
                        RePoeImportDiagnosticCodes.TagRecordMalformed,
                        ImportDiagnosticSeverity.Warning,
                        $"tags[{read - 1}]",
                        "RePoE tag id is unusable and was skipped."));
                    continue;
                }

                var id = sourceRecord.GetString()!.Trim();
                if (!seen.Add(id))
                {
                    skipped++;
                    diagnostics.Add(new ImportDiagnostic(
                        RePoeImportDiagnosticCodes.TagRecordDuplicate,
                        ImportDiagnosticSeverity.Warning,
                        id,
                        "RePoE tag id is duplicated and the later entry was skipped."));
                    continue;
                }

                records.Add(new TagDefinition
                {
                    Id = id,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = RePoeBaseItemImporter.SourceId,
                            ExternalId = id,
                        },
                    ],
                });
            }

            var ordered = records.OrderBy(record => record.Id, StringComparer.Ordinal).ToArray();
            return new ImportResult<TagDefinition>
            {
                ImportedRecords = ordered,
                Diagnostics = diagnostics,
                SourceRecordsRead = read,
                RecordsImported = ordered.Length,
                RecordsSkipped = skipped,
            };
        }
        catch (JsonException exception)
        {
            return Failure(
                RePoeImportDiagnosticCodes.JsonMalformed,
                $"RePoE tags.json could not be parsed as JSON: {exception.Message}");
        }
    }

    private static ImportResult<TagDefinition> Failure(string code, string message) => new()
    {
        Diagnostics = [new ImportDiagnostic(code, ImportDiagnosticSeverity.Error, null, message)],
    };
}

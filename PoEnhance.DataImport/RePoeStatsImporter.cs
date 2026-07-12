using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeStatsImporter
{
    public ImportResult<StatDefinition> Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ImportResult<StatDefinition>
            {
                Diagnostics =
                [
                    Diagnostic(
                        RePoeImportDiagnosticCodes.FileNotFound,
                        ImportDiagnosticSeverity.Error,
                        null,
                        $"RePoE stats.json file was not found: {filePath}"),
                ],
            };
        }

        using var stream = File.OpenRead(filePath);
        return Import(stream);
    }

    public ImportResult<StatDefinition> Import(Stream stream)
    {
        try
        {
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return SchemaUnsupported("RePoE stats.json root must be a JSON object keyed by stat id.");
            }

            return ImportRootObject(document.RootElement);
        }
        catch (JsonException exception)
        {
            return new ImportResult<StatDefinition>
            {
                Diagnostics =
                [
                    Diagnostic(
                        RePoeImportDiagnosticCodes.JsonMalformed,
                        ImportDiagnosticSeverity.Error,
                        null,
                        $"RePoE stats.json could not be parsed as JSON: {exception.Message}"),
                ],
            };
        }
    }

    private static ImportResult<StatDefinition> ImportRootObject(JsonElement root)
    {
        var importedRecords = new List<StatDefinition>();
        var diagnostics = new List<ImportDiagnostic>();
        var sourceRecordsRead = 0;
        var recordsSkipped = 0;

        foreach (var sourceRecord in root.EnumerateObject())
        {
            sourceRecordsRead++;

            var importedRecord = TryImportRecord(sourceRecord, diagnostics);
            if (importedRecord is null)
            {
                recordsSkipped++;
                continue;
            }

            importedRecords.Add(importedRecord);
        }

        var orderedRecords = importedRecords
            .OrderBy(record => record.Id, StringComparer.Ordinal)
            .ToArray();

        return new ImportResult<StatDefinition>
        {
            ImportedRecords = orderedRecords,
            Diagnostics = diagnostics,
            SourceRecordsRead = sourceRecordsRead,
            RecordsImported = orderedRecords.Length,
            RecordsSkipped = recordsSkipped,
        };
    }

    private static StatDefinition? TryImportRecord(
        JsonProperty sourceRecord,
        List<ImportDiagnostic> diagnostics)
    {
        var sourceRecordId = sourceRecord.Name.Trim();
        if (string.IsNullOrWhiteSpace(sourceRecordId))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.StatRecordMissingId,
                ImportDiagnosticSeverity.Warning,
                null,
                "RePoE stat record has an empty stat id and was skipped."));
            return null;
        }

        if (sourceRecord.Value.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.StatRecordUnsupported,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE stat record value must be an object and was skipped."));
            return null;
        }

        var record = sourceRecord.Value;
        if (!record.TryGetProperty("is_local", out var isLocalProperty) ||
            isLocalProperty.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.StatRecordMissingIsLocal,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE stat record is missing a boolean is_local value and was skipped."));
            return null;
        }

        if (!TryReadAliases(record, sourceRecordId, diagnostics, out var mainHandAliasId, out var offHandAliasId))
        {
            return null;
        }

        return new StatDefinition
        {
            Id = sourceRecordId,
            IsLocal = isLocalProperty.GetBoolean(),
            MainHandAliasId = mainHandAliasId,
            OffHandAliasId = offHandAliasId,
            Sources = [CreateSourceReference(sourceRecordId)],
        };
    }

    private static bool TryReadAliases(
        JsonElement record,
        string sourceRecordId,
        List<ImportDiagnostic> diagnostics,
        out string? mainHandAliasId,
        out string? offHandAliasId)
    {
        mainHandAliasId = null;
        offHandAliasId = null;

        if (!record.TryGetProperty("alias", out var aliases) || aliases.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (aliases.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.StatRecordInvalidAlias,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE stat record has a non-object alias value and was skipped."));
            return false;
        }

        foreach (var alias in aliases.EnumerateObject())
        {
            if (alias.Name is not "when_in_main_hand" and not "when_in_off_hand" ||
                alias.Value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(alias.Value.GetString()))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.StatRecordInvalidAlias,
                    ImportDiagnosticSeverity.Warning,
                    sourceRecordId,
                    "RePoE stat record has an unsupported alias entry and was skipped."));
                return false;
            }

            if (alias.Name == "when_in_main_hand")
            {
                mainHandAliasId = alias.Value.GetString()!.Trim();
            }
            else
            {
                offHandAliasId = alias.Value.GetString()!.Trim();
            }
        }

        return true;
    }

    private static ImportResult<StatDefinition> SchemaUnsupported(string message)
    {
        return new ImportResult<StatDefinition>
        {
            Diagnostics =
            [
                Diagnostic(
                    RePoeImportDiagnosticCodes.SchemaUnsupported,
                    ImportDiagnosticSeverity.Error,
                    null,
                    message),
            ],
        };
    }

    private static GameDataSourceReference CreateSourceReference(string sourceRecordId)
    {
        return new GameDataSourceReference
        {
            SourceId = RePoeBaseItemImporter.SourceId,
            ExternalId = sourceRecordId,
        };
    }

    private static ImportDiagnostic Diagnostic(
        string code,
        ImportDiagnosticSeverity severity,
        string? sourceRecordId,
        string message)
    {
        return new ImportDiagnostic(code, severity, sourceRecordId, message);
    }
}

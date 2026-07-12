using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeBaseItemImporter
{
    public const string SourceId = "repoe";

    private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;

    public ImportResult<ItemBaseRecord> Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ImportResult<ItemBaseRecord>
            {
                Diagnostics =
                [
                    Diagnostic(
                        RePoeImportDiagnosticCodes.FileNotFound,
                        ImportDiagnosticSeverity.Error,
                        null,
                        $"RePoE base_items.json file was not found: {filePath}"),
                ],
            };
        }

        using var stream = File.OpenRead(filePath);
        return Import(stream);
    }

    public ImportResult<ItemBaseRecord> Import(Stream stream)
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
                return SchemaUnsupported("RePoE base_items.json root must be a JSON object keyed by metadata id.");
            }

            return ImportRootObject(document.RootElement);
        }
        catch (JsonException exception)
        {
            return new ImportResult<ItemBaseRecord>
            {
                Diagnostics =
                [
                    Diagnostic(
                        RePoeImportDiagnosticCodes.JsonMalformed,
                        ImportDiagnosticSeverity.Error,
                        null,
                        $"RePoE base_items.json could not be parsed as JSON: {exception.Message}"),
                ],
            };
        }
    }

    private static ImportResult<ItemBaseRecord> ImportRootObject(JsonElement root)
    {
        var importedRecords = new List<ItemBaseRecord>();
        var diagnostics = new List<ImportDiagnostic>();
        var sourceRecordsRead = 0;
        var recordsSkipped = 0;

        foreach (var sourceRecord in root.EnumerateObject())
        {
            sourceRecordsRead++;

            var importRecordResult = TryImportRecord(sourceRecord, diagnostics);
            if (importRecordResult is null)
            {
                recordsSkipped++;
                continue;
            }

            importedRecords.Add(importRecordResult);
        }

        var orderedRecords = importedRecords
            .OrderBy(record => record.Id, StringComparer.Ordinal)
            .ToArray();

        return new ImportResult<ItemBaseRecord>
        {
            ImportedRecords = orderedRecords,
            Diagnostics = diagnostics,
            SourceRecordsRead = sourceRecordsRead,
            RecordsImported = orderedRecords.Length,
            RecordsSkipped = recordsSkipped,
        };
    }

    private static ItemBaseRecord? TryImportRecord(
        JsonProperty sourceRecord,
        List<ImportDiagnostic> diagnostics)
    {
        var sourceRecordId = sourceRecord.Name.Trim();
        if (string.IsNullOrWhiteSpace(sourceRecordId))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.RecordMissingId,
                ImportDiagnosticSeverity.Warning,
                null,
                "RePoE base item record has an empty metadata id and was skipped."));
            return null;
        }

        if (sourceRecord.Value.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.RecordUnsupported,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE base item record value must be an object and was skipped."));
            return null;
        }

        var record = sourceRecord.Value;
        var name = ReadRequiredString(record, "name");
        if (name is null)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.RecordMissingName,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE base item record is missing a usable name and was skipped."));
            return null;
        }

        var itemClass = ReadRequiredString(record, "item_class");
        if (itemClass is null)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.RecordMissingItemClass,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE base item record is missing a usable item_class and was skipped."));
            return null;
        }

        var requiredLevel = ReadRequiredLevel(record, sourceRecordId, diagnostics, out var hasInvalidRequiredLevel);
        if (hasInvalidRequiredLevel)
        {
            return null;
        }

        return new ItemBaseRecord
        {
            Id = sourceRecordId,
            Name = name,
            ItemClass = itemClass,
            RequiredLevel = requiredLevel,
            Domain = ReadOptionalString(record, "domain"),
            Tags = ReadTags(record, sourceRecordId, diagnostics),
            Sources = [CreateSourceReference(sourceRecordId)],
        };
    }

    private static string? ReadRequiredString(JsonElement record, string propertyName)
    {
        if (!record.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadOptionalString(JsonElement record, string propertyName)
    {
        if (!record.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? ReadRequiredLevel(
        JsonElement record,
        string sourceRecordId,
        List<ImportDiagnostic> diagnostics,
        out bool hasInvalidRequiredLevel)
    {
        hasInvalidRequiredLevel = false;

        if (!record.TryGetProperty("requirements", out var requirements) ||
            requirements.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (requirements.ValueKind != JsonValueKind.Object ||
            !requirements.TryGetProperty("level", out var level) ||
            level.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (level.ValueKind == JsonValueKind.Number &&
            level.TryGetInt32(out var requiredLevel) &&
            requiredLevel >= 0)
        {
            return requiredLevel;
        }

        diagnostics.Add(Diagnostic(
            RePoeImportDiagnosticCodes.RecordInvalidRequiredLevel,
            ImportDiagnosticSeverity.Warning,
            sourceRecordId,
            "RePoE base item record has an invalid requirements.level value and was skipped."));
        hasInvalidRequiredLevel = true;
        return null;
    }

    private static IReadOnlyList<string> ReadTags(
        JsonElement record,
        string sourceRecordId,
        List<ImportDiagnostic> diagnostics)
    {
        if (!record.TryGetProperty("tags", out var tags) || tags.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (tags.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.RecordInvalidTags,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE base item record has a non-array tags value; tags were ignored."));
            return [];
        }

        var normalizedTags = new Dictionary<string, string>(TagComparer);
        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.RecordInvalidTags,
                    ImportDiagnosticSeverity.Warning,
                    sourceRecordId,
                    "RePoE base item record has a non-string tag value; that tag was ignored."));
                continue;
            }

            var normalizedTag = tag.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTag))
            {
                continue;
            }

            normalizedTags.TryAdd(normalizedTag, normalizedTag);
        }

        return normalizedTags.Values
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
    }

    private static ImportResult<ItemBaseRecord> SchemaUnsupported(string message)
    {
        return new ImportResult<ItemBaseRecord>
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
            SourceId = SourceId,
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

using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeItemClassImporter
{
    public ImportResult<ItemClassDefinition> Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return MissingFile<ItemClassDefinition>(filePath, "item_classes.json");
        }

        using var stream = File.OpenRead(filePath);
        return Import(stream);
    }

    public ImportResult<ItemClassDefinition> Import(Stream stream)
    {
        try
        {
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return SchemaUnsupported<ItemClassDefinition>(
                    "RePoE item_classes.json root must be an object keyed by item-class id.");
            }

            var records = new List<ItemClassDefinition>();
            var diagnostics = new List<ImportDiagnostic>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var read = 0;
            var skipped = 0;
            foreach (var sourceRecord in document.RootElement.EnumerateObject())
            {
                read++;
                var id = sourceRecord.Name.Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    skipped++;
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.ItemClassRecordMissingId,
                        ImportDiagnosticSeverity.Warning,
                        null,
                        "RePoE item-class record has an empty id and was skipped."));
                    continue;
                }

                if (!seenIds.Add(id))
                {
                    skipped++;
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.ItemClassRecordDuplicate,
                        ImportDiagnosticSeverity.Warning,
                        id,
                        "RePoE item-class id is duplicated and the later record was skipped."));
                    continue;
                }

                if (!TryReadRecord(sourceRecord.Value, id, diagnostics, out var record))
                {
                    skipped++;
                    continue;
                }

                records.Add(record!);
            }

            var ordered = records.OrderBy(record => record.Id, StringComparer.Ordinal).ToArray();
            return new ImportResult<ItemClassDefinition>
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
            return MalformedJson<ItemClassDefinition>("item_classes.json", exception);
        }
    }

    private static bool TryReadRecord(
        JsonElement source,
        string id,
        List<ImportDiagnostic> diagnostics,
        out ItemClassDefinition? record)
    {
        record = null;
        if (source.ValueKind != JsonValueKind.Object ||
            !TryReadOptionalString(source, "name", out var name) ||
            !TryReadOptionalString(source, "category_id", out var categoryId) ||
            !TryReadOptionalString(source, "category", out var categoryName) ||
            !TryReadInfluenceTags(source, out var influenceTags))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.ItemClassRecordMalformed,
                ImportDiagnosticSeverity.Warning,
                id,
                "RePoE item-class record is malformed and was skipped."));
            return false;
        }

        record = new ItemClassDefinition
        {
            Id = id,
            Name = name,
            CategoryId = categoryId,
            CategoryName = categoryName,
            InfluenceTagIds = influenceTags,
            Sources = [Source(id)],
        };
        return true;
    }

    private static bool TryReadInfluenceTags(JsonElement source, out IReadOnlyList<string> tags)
    {
        tags = [];
        if (!source.TryGetProperty("influence_tags", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var imported = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()) ||
                !seen.Add(item.GetString()!.Trim()))
            {
                return false;
            }

            imported.Add(item.GetString()!.Trim());
        }

        tags = imported;
        return true;
    }

    private static bool TryReadOptionalString(JsonElement source, string property, out string? value)
    {
        value = null;
        if (!source.TryGetProperty(property, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
        }

        return true;
    }

    private static GameDataSourceReference Source(string id) => new()
    {
        SourceId = RePoeBaseItemImporter.SourceId,
        ExternalId = id,
    };

    private static ImportResult<T> MissingFile<T>(string path, string label) => new()
    {
        Diagnostics = [Diagnostic(RePoeImportDiagnosticCodes.FileNotFound, ImportDiagnosticSeverity.Error, null,
            $"RePoE {label} file was not found: {path}")],
    };

    private static ImportResult<T> SchemaUnsupported<T>(string message) => new()
    {
        Diagnostics = [Diagnostic(RePoeImportDiagnosticCodes.SchemaUnsupported, ImportDiagnosticSeverity.Error, null, message)],
    };

    private static ImportResult<T> MalformedJson<T>(string label, JsonException exception) => new()
    {
        Diagnostics = [Diagnostic(RePoeImportDiagnosticCodes.JsonMalformed, ImportDiagnosticSeverity.Error, null,
            $"RePoE {label} could not be parsed as JSON: {exception.Message}")],
    };

    private static ImportDiagnostic Diagnostic(string code, ImportDiagnosticSeverity severity, string? id, string message) =>
        new(code, severity, id, message);
}

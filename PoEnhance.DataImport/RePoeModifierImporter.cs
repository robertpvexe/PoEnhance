using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeModifierImporter
{
    private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;

    public ImportResult<ModifierDefinition> Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ImportResult<ModifierDefinition>
            {
                Diagnostics =
                [
                    Diagnostic(
                        RePoeImportDiagnosticCodes.FileNotFound,
                        ImportDiagnosticSeverity.Error,
                        null,
                        $"RePoE mods.json file was not found: {filePath}"),
                ],
            };
        }

        using var stream = File.OpenRead(filePath);
        return Import(stream);
    }

    public ImportResult<ModifierDefinition> Import(Stream stream)
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
                return SchemaUnsupported("RePoE mods.json root must be a JSON object keyed by modifier id.");
            }

            return ImportRootObject(document.RootElement);
        }
        catch (JsonException exception)
        {
            return new ImportResult<ModifierDefinition>
            {
                Diagnostics =
                [
                    Diagnostic(
                        RePoeImportDiagnosticCodes.JsonMalformed,
                        ImportDiagnosticSeverity.Error,
                        null,
                        $"RePoE mods.json could not be parsed as JSON: {exception.Message}"),
                ],
            };
        }
    }

    private static ImportResult<ModifierDefinition> ImportRootObject(JsonElement root)
    {
        var importedRecords = new List<ModifierDefinition>();
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

        return new ImportResult<ModifierDefinition>
        {
            ImportedRecords = orderedRecords,
            Diagnostics = diagnostics,
            SourceRecordsRead = sourceRecordsRead,
            RecordsImported = orderedRecords.Length,
            RecordsSkipped = recordsSkipped,
        };
    }

    private static ModifierDefinition? TryImportRecord(
        JsonProperty sourceRecord,
        List<ImportDiagnostic> diagnostics)
    {
        var sourceRecordId = sourceRecord.Name.Trim();
        if (string.IsNullOrWhiteSpace(sourceRecordId))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.ModifierRecordMissingId,
                ImportDiagnosticSeverity.Warning,
                null,
                "RePoE modifier record has an empty modifier id and was skipped."));
            return null;
        }

        if (sourceRecord.Value.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.ModifierRecordUnsupported,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE modifier record value must be an object and was skipped."));
            return null;
        }

        var record = sourceRecord.Value;
        var groupId = ReadGroupId(record);
        if (groupId is null)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.ModifierRecordMissingGroup,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE modifier record is missing a usable group or type and was skipped."));
            return null;
        }

        var stats = ReadStats(record, sourceRecordId, diagnostics, out var invalidStats);
        if (invalidStats)
        {
            return null;
        }

        if (stats.Count == 0)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.ModifierRecordMissingStats,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE modifier record has no stats and was skipped."));
            return null;
        }

        return new ModifierDefinition
        {
            Id = sourceRecordId,
            GroupId = groupId,
            Name = ReadOptionalString(record, "name"),
            GenerationType = ReadGenerationType(record),
            SourceGenerationType = ReadOptionalString(record, "generation_type"),
            RequiredLevel = ReadOptionalNonNegativeInt(record, "required_level"),
            Domain = ReadOptionalString(record, "domain"),
            IsEssenceOnly = ReadOptionalBoolean(record, "is_essence_only"),
            Tags = ReadTags(record, sourceRecordId, diagnostics),
            Stats = stats,
            SpawnWeights = ReadSpawnWeights(record, sourceRecordId, diagnostics),
            Sources = [CreateSourceReference(sourceRecordId)],
        };
    }

    private static string? ReadGroupId(JsonElement record)
    {
        if (record.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in groups.EnumerateArray())
            {
                if (group.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(group.GetString()))
                {
                    return group.GetString()!.Trim();
                }
            }
        }

        return ReadOptionalString(record, "type");
    }

    private static ModifierGenerationType ReadGenerationType(JsonElement record)
    {
        var generationType = ReadOptionalString(record, "generation_type");
        return generationType switch
        {
            "prefix" => ModifierGenerationType.Prefix,
            "suffix" => ModifierGenerationType.Suffix,
            "enchantment" => ModifierGenerationType.Enchantment,
            "unique" => ModifierGenerationType.Implicit,
            "exarch_implicit" => ModifierGenerationType.Implicit,
            "searing_exarch_implicit" => ModifierGenerationType.Implicit,
            "eater_implicit" => ModifierGenerationType.Implicit,
            "eater_of_worlds_implicit" => ModifierGenerationType.Implicit,
            _ => ModifierGenerationType.Unknown,
        };
    }

    private static IReadOnlyList<ModifierStat> ReadStats(
        JsonElement record,
        string sourceRecordId,
        List<ImportDiagnostic> diagnostics,
        out bool invalidStats)
    {
        invalidStats = false;

        if (!record.TryGetProperty("stats", out var stats) || stats.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var importedStats = new List<ModifierStat>();
        var index = 0;
        foreach (var stat in stats.EnumerateArray())
        {
            if (stat.ValueKind != JsonValueKind.Object ||
                !stat.TryGetProperty("id", out var statIdElement) ||
                statIdElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(statIdElement.GetString()) ||
                !TryReadDecimal(stat, "min", out var minValue) ||
                !TryReadDecimal(stat, "max", out var maxValue) ||
                (minValue.HasValue && maxValue.HasValue && minValue.Value > maxValue.Value))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.ModifierRecordInvalidStat,
                    ImportDiagnosticSeverity.Warning,
                    sourceRecordId,
                    "RePoE modifier record has an invalid stat entry and was skipped."));
                invalidStats = true;
                return [];
            }

            importedStats.Add(new ModifierStat
            {
                Index = index,
                StatId = statIdElement.GetString()!.Trim(),
                MinValue = minValue,
                MaxValue = maxValue,
            });

            index++;
        }

        return importedStats;
    }

    private static IReadOnlyList<string> ReadTags(
        JsonElement record,
        string sourceRecordId,
        List<ImportDiagnostic> diagnostics)
    {
        var normalizedTags = new Dictionary<string, string>(TagComparer);
        ReadTagsFromProperty(record, "adds_tags", sourceRecordId, diagnostics, normalizedTags);
        ReadTagsFromProperty(record, "implicit_tags", sourceRecordId, diagnostics, normalizedTags);

        return normalizedTags.Values
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ReadTagsFromProperty(
        JsonElement record,
        string propertyName,
        string sourceRecordId,
        List<ImportDiagnostic> diagnostics,
        Dictionary<string, string> normalizedTags)
    {
        if (!record.TryGetProperty(propertyName, out var tags) || tags.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (tags.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.ModifierRecordInvalidTags,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                $"RePoE modifier record has a non-array {propertyName} value; tags were ignored."));
            return;
        }

        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.ModifierRecordInvalidTags,
                    ImportDiagnosticSeverity.Warning,
                    sourceRecordId,
                    $"RePoE modifier record has a non-string {propertyName} tag; that tag was ignored."));
                continue;
            }

            var normalizedTag = tag.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedTag))
            {
                normalizedTags.TryAdd(normalizedTag, normalizedTag);
            }
        }
    }

    private static IReadOnlyList<ModifierSpawnWeight> ReadSpawnWeights(
        JsonElement record,
        string sourceRecordId,
        List<ImportDiagnostic> diagnostics)
    {
        if (!record.TryGetProperty("spawn_weights", out var spawnWeights) ||
            spawnWeights.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (spawnWeights.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.ModifierRecordInvalidSpawnWeight,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE modifier record has a non-array spawn_weights value; spawn weights were ignored."));
            return [];
        }

        var importedSpawnWeights = new List<ModifierSpawnWeight>();
        foreach (var spawnWeight in spawnWeights.EnumerateArray())
        {
            if (spawnWeight.ValueKind != JsonValueKind.Object ||
                !spawnWeight.TryGetProperty("tag", out var tagElement) ||
                tagElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(tagElement.GetString()) ||
                !spawnWeight.TryGetProperty("weight", out var weightElement) ||
                weightElement.ValueKind != JsonValueKind.Number ||
                !weightElement.TryGetInt32(out var weight) ||
                weight < 0)
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.ModifierRecordInvalidSpawnWeight,
                    ImportDiagnosticSeverity.Warning,
                    sourceRecordId,
                    "RePoE modifier record has an invalid spawn weight entry; that entry was ignored."));
                continue;
            }

            importedSpawnWeights.Add(new ModifierSpawnWeight
            {
                Tag = tagElement.GetString()!.Trim(),
                Weight = weight,
            });
        }

        return importedSpawnWeights.ToArray();
    }

    private static int? ReadOptionalNonNegativeInt(JsonElement record, string propertyName)
    {
        if (!record.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var value) &&
            value >= 0
            ? value
            : null;
    }

    private static string? ReadOptionalString(JsonElement record, string propertyName)
    {
        if (!record.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool ReadOptionalBoolean(JsonElement record, string propertyName)
    {
        return record.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            property.GetBoolean();
    }

    private static bool TryReadDecimal(JsonElement element, string propertyName, out decimal? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDecimal(out var decimalValue))
        {
            return false;
        }

        value = decimalValue;
        return true;
    }

    private static ImportResult<ModifierDefinition> SchemaUnsupported(string message)
    {
        return new ImportResult<ModifierDefinition>
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

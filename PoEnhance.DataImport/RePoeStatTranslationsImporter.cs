using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeStatTranslationsImporter
{
    private const string LanguagePropertyName = "English";

    public ImportResult<StatTranslationDefinition> Import(string filePath)
    {
        return Import(filePath, knownStats: null);
    }

    public ImportResult<StatTranslationDefinition> Import(
        string filePath,
        IReadOnlyCollection<StatDefinition>? knownStats)
    {
        if (!File.Exists(filePath))
        {
            return new ImportResult<StatTranslationDefinition>
            {
                Diagnostics =
                [
                    Diagnostic(
                        RePoeImportDiagnosticCodes.FileNotFound,
                        ImportDiagnosticSeverity.Error,
                        null,
                        $"RePoE stat_translations.json file was not found: {filePath}"),
                ],
            };
        }

        using var stream = File.OpenRead(filePath);
        return Import(stream, knownStats);
    }

    public ImportResult<StatTranslationDefinition> Import(Stream stream)
    {
        return Import(stream, knownStats: null);
    }

    public ImportResult<StatTranslationDefinition> Import(
        Stream stream,
        IReadOnlyCollection<StatDefinition>? knownStats)
    {
        try
        {
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return SchemaUnsupported("RePoE stat_translations.json root must be a JSON array.");
            }

            return ImportRootArray(document.RootElement, knownStats);
        }
        catch (JsonException exception)
        {
            return new ImportResult<StatTranslationDefinition>
            {
                Diagnostics =
                [
                    Diagnostic(
                        RePoeImportDiagnosticCodes.JsonMalformed,
                        ImportDiagnosticSeverity.Error,
                        null,
                        $"RePoE stat_translations.json could not be parsed as JSON: {exception.Message}"),
                ],
            };
        }
    }

    private static ImportResult<StatTranslationDefinition> ImportRootArray(
        JsonElement root,
        IReadOnlyCollection<StatDefinition>? knownStats)
    {
        var importedRecords = new List<StatTranslationDefinition>();
        var diagnostics = new List<ImportDiagnostic>();
        var knownStatIds = BuildKnownStatIds(knownStats);
        var sourceRecordsRead = 0;
        var recordsSkipped = 0;

        foreach (var sourceRecord in root.EnumerateArray())
        {
            var sourceIndex = sourceRecordsRead;
            sourceRecordsRead++;

            var importedRecord = TryImportRecord(sourceRecord, sourceIndex, knownStatIds, diagnostics);
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

        return new ImportResult<StatTranslationDefinition>
        {
            ImportedRecords = orderedRecords,
            Diagnostics = diagnostics,
            SourceRecordsRead = sourceRecordsRead,
            RecordsImported = orderedRecords.Length,
            RecordsSkipped = recordsSkipped,
        };
    }

    private static StatTranslationDefinition? TryImportRecord(
        JsonElement sourceRecord,
        int sourceIndex,
        ISet<string>? knownStatIds,
        List<ImportDiagnostic> diagnostics)
    {
        var sourceRecordId = $"stat_translations[{sourceIndex}]";
        if (sourceRecord.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.StatTranslationRecordUnsupported,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE stat translation record value must be an object and was skipped."));
            return null;
        }

        if (!TryReadStatIds(sourceRecord, sourceRecordId, knownStatIds, diagnostics, out var statIds))
        {
            return null;
        }

        if (!sourceRecord.TryGetProperty(LanguagePropertyName, out var variantsElement) ||
            variantsElement.ValueKind != JsonValueKind.Array ||
            variantsElement.GetArrayLength() == 0)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.StatTranslationMissingVariants,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE stat translation record is missing English variants and was skipped."));
            return null;
        }

        var variants = new List<StatTranslationVariant>();
        var variantIndex = 0;
        foreach (var variantElement in variantsElement.EnumerateArray())
        {
            var variant = TryImportVariant(variantElement, sourceRecordId, variantIndex, statIds.Count, diagnostics);
            if (variant is null)
            {
                return null;
            }

            variants.Add(variant);
            variantIndex++;
        }

        var id = CreateDeterministicId(statIds);
        return new StatTranslationDefinition
        {
            Id = id,
            StatIds = statIds,
            Language = LanguagePropertyName,
            Variants = variants,
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = RePoeBaseItemImporter.SourceId,
                    ExternalId = id,
                },
            ],
        };
    }

    private static bool TryReadStatIds(
        JsonElement sourceRecord,
        string sourceRecordId,
        ISet<string>? knownStatIds,
        List<ImportDiagnostic> diagnostics,
        out IReadOnlyList<string> statIds)
    {
        statIds = [];

        if (!sourceRecord.TryGetProperty("ids", out var idsElement) ||
            idsElement.ValueKind != JsonValueKind.Array ||
            idsElement.GetArrayLength() == 0)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.StatTranslationMissingStatIds,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                "RePoE stat translation record is missing ids and was skipped."));
            return false;
        }

        var importedStatIds = new List<string>();
        var seenStatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var idElement in idsElement.EnumerateArray())
        {
            if (idElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(idElement.GetString()))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.StatTranslationMissingStatIds,
                    ImportDiagnosticSeverity.Warning,
                    sourceRecordId,
                    "RePoE stat translation record has an empty stat id and was skipped."));
                return false;
            }

            var statId = idElement.GetString()!.Trim();
            if (!seenStatIds.Add(statId))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.StatTranslationDuplicateStatId,
                    ImportDiagnosticSeverity.Warning,
                    sourceRecordId,
                    $"RePoE stat translation record has duplicate stat id '{statId}' and was skipped."));
                return false;
            }

            if (knownStatIds is { Count: > 0 } && !knownStatIds.Contains(statId))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.StatTranslationUnknownStatId,
                    ImportDiagnosticSeverity.Warning,
                    sourceRecordId,
                    $"RePoE stat translation record references unknown stat id '{statId}' and was skipped."));
                return false;
            }

            importedStatIds.Add(statId);
        }

        statIds = importedStatIds;
        return true;
    }

    private static StatTranslationVariant? TryImportVariant(
        JsonElement variantElement,
        string sourceRecordId,
        int variantIndex,
        int statIdCount,
        List<ImportDiagnostic> diagnostics)
    {
        if (variantElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.StatTranslationRecordUnsupported,
                ImportDiagnosticSeverity.Warning,
                sourceRecordId,
                $"RePoE stat translation variant {variantIndex} must be an object and was skipped."));
            return null;
        }

        if (!TryReadConditions(variantElement, sourceRecordId, variantIndex, statIdCount, diagnostics, out var conditions) ||
            !TryReadValueFormats(variantElement, sourceRecordId, variantIndex, diagnostics, out var valueFormats) ||
            !TryReadIndexHandlers(variantElement, sourceRecordId, variantIndex, statIdCount, diagnostics, out var indexHandlers) ||
            !TryReadFormatLines(variantElement, sourceRecordId, variantIndex, diagnostics, out var formatLines))
        {
            return null;
        }

        return new StatTranslationVariant
        {
            Conditions = conditions,
            ValueFormats = valueFormats,
            IndexHandlers = indexHandlers,
            FormatLines = formatLines,
        };
    }

    private static bool TryReadConditions(
        JsonElement variantElement,
        string sourceRecordId,
        int variantIndex,
        int statIdCount,
        List<ImportDiagnostic> diagnostics,
        out IReadOnlyList<StatTranslationCondition> conditions)
    {
        conditions = [];

        if (!variantElement.TryGetProperty("condition", out var conditionsElement) ||
            conditionsElement.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(InvalidConditionDiagnostic(sourceRecordId, variantIndex));
            return false;
        }

        var importedConditions = new List<StatTranslationCondition>();
        var index = 0;
        foreach (var conditionElement in conditionsElement.EnumerateArray())
        {
            if (index >= statIdCount || conditionElement.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(InvalidConditionDiagnostic(sourceRecordId, variantIndex));
                return false;
            }

            if (!TryReadDecimal(conditionElement, "min", out var minValue) ||
                !TryReadDecimal(conditionElement, "max", out var maxValue) ||
                !TryReadNullableBoolean(conditionElement, "negated") ||
                (minValue.HasValue && maxValue.HasValue && minValue.Value > maxValue.Value))
            {
                diagnostics.Add(InvalidConditionDiagnostic(sourceRecordId, variantIndex));
                return false;
            }

            importedConditions.Add(new StatTranslationCondition
            {
                Index = index,
                MinValue = minValue,
                MaxValue = maxValue,
            });

            index++;
        }

        conditions = importedConditions;
        return true;
    }

    private static bool TryReadValueFormats(
        JsonElement variantElement,
        string sourceRecordId,
        int variantIndex,
        List<ImportDiagnostic> diagnostics,
        out IReadOnlyList<string> valueFormats)
    {
        valueFormats = [];

        if (!variantElement.TryGetProperty("format", out var formatElement) ||
            formatElement.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(InvalidFormatDiagnostic(sourceRecordId, variantIndex));
            return false;
        }

        var importedFormats = new List<string>();
        foreach (var format in formatElement.EnumerateArray())
        {
            if (format.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(format.GetString()))
            {
                diagnostics.Add(InvalidFormatDiagnostic(sourceRecordId, variantIndex));
                return false;
            }

            importedFormats.Add(format.GetString()!.Trim());
        }

        valueFormats = importedFormats;
        return true;
    }

    private static bool TryReadIndexHandlers(
        JsonElement variantElement,
        string sourceRecordId,
        int variantIndex,
        int statIdCount,
        List<ImportDiagnostic> diagnostics,
        out IReadOnlyList<StatTranslationIndexHandler> indexHandlers)
    {
        indexHandlers = [];

        if (!variantElement.TryGetProperty("index_handlers", out var handlersElement) ||
            handlersElement.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(InvalidIndexHandlerDiagnostic(sourceRecordId, variantIndex));
            return false;
        }

        var importedHandlers = new List<StatTranslationIndexHandler>();
        var index = 0;
        foreach (var handlerGroup in handlersElement.EnumerateArray())
        {
            if (index >= statIdCount || handlerGroup.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(InvalidIndexHandlerDiagnostic(sourceRecordId, variantIndex));
                return false;
            }

            var handlers = new List<string>();
            foreach (var handler in handlerGroup.EnumerateArray())
            {
                if (handler.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(handler.GetString()))
                {
                    diagnostics.Add(InvalidIndexHandlerDiagnostic(sourceRecordId, variantIndex));
                    return false;
                }

                handlers.Add(handler.GetString()!.Trim());
            }

            importedHandlers.Add(new StatTranslationIndexHandler
            {
                Index = index,
                Handlers = handlers,
            });

            index++;
        }

        indexHandlers = importedHandlers;
        return true;
    }

    private static bool TryReadFormatLines(
        JsonElement variantElement,
        string sourceRecordId,
        int variantIndex,
        List<ImportDiagnostic> diagnostics,
        out IReadOnlyList<string> formatLines)
    {
        formatLines = [];

        if (!variantElement.TryGetProperty("string", out var stringElement) ||
            stringElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(stringElement.GetString()))
        {
            diagnostics.Add(InvalidFormatDiagnostic(sourceRecordId, variantIndex));
            return false;
        }

        var lines = stringElement.GetString()!
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();

        if (lines.Any(string.IsNullOrWhiteSpace))
        {
            diagnostics.Add(InvalidFormatDiagnostic(sourceRecordId, variantIndex));
            return false;
        }

        formatLines = lines;
        return true;
    }

    private static bool TryReadDecimal(JsonElement element, string propertyName, out decimal? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.Null)
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

    private static bool TryReadNullableBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return property.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    private static ISet<string>? BuildKnownStatIds(IReadOnlyCollection<StatDefinition>? knownStats)
    {
        if (knownStats is null || knownStats.Count == 0)
        {
            return null;
        }

        return knownStats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.Id))
            .Select(stat => stat.Id!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string CreateDeterministicId(IReadOnlyList<string> statIds)
    {
        var stableKey = string.Join('\u001F', statIds);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableKey));
        return "repoe:stat-translation:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static ImportResult<StatTranslationDefinition> SchemaUnsupported(string message)
    {
        return new ImportResult<StatTranslationDefinition>
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

    private static ImportDiagnostic InvalidConditionDiagnostic(string sourceRecordId, int variantIndex)
    {
        return Diagnostic(
            RePoeImportDiagnosticCodes.StatTranslationInvalidCondition,
            ImportDiagnosticSeverity.Warning,
            sourceRecordId,
            $"RePoE stat translation variant {variantIndex} has an invalid condition array and was skipped.");
    }

    private static ImportDiagnostic InvalidFormatDiagnostic(string sourceRecordId, int variantIndex)
    {
        return Diagnostic(
            RePoeImportDiagnosticCodes.StatTranslationInvalidFormat,
            ImportDiagnosticSeverity.Warning,
            sourceRecordId,
            $"RePoE stat translation variant {variantIndex} has an invalid format or string value and was skipped.");
    }

    private static ImportDiagnostic InvalidIndexHandlerDiagnostic(string sourceRecordId, int variantIndex)
    {
        return Diagnostic(
            RePoeImportDiagnosticCodes.StatTranslationInvalidIndexHandler,
            ImportDiagnosticSeverity.Warning,
            sourceRecordId,
            $"RePoE stat translation variant {variantIndex} has an invalid index_handlers array and was skipped.");
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

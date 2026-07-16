using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class ReviewedItemPropertySemanticImporter
{
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public ImportResult<ItemPropertySemanticDescriptor> Import(string filePath)
    {
        return Import(filePath, knownStats: null);
    }

    public ImportResult<ItemPropertySemanticDescriptor> Import(
        string filePath,
        IReadOnlyCollection<StatDefinition>? knownStats)
    {
        if (!File.Exists(filePath))
        {
            return Failure(
                ItemPropertySemanticImportDiagnosticCodes.FileNotFound,
                null,
                $"Reviewed item-property semantic file was not found: {filePath}");
        }

        using var stream = File.OpenRead(filePath);
        return Import(stream, knownStats);
    }

    public ImportResult<ItemPropertySemanticDescriptor> Import(Stream stream)
    {
        return Import(stream, knownStats: null);
    }

    public ImportResult<ItemPropertySemanticDescriptor> Import(
        Stream stream,
        IReadOnlyCollection<StatDefinition>? knownStats)
    {
        ReviewedItemPropertySemanticDataSet? dataSet;
        try
        {
            dataSet = JsonSerializer.Deserialize<ReviewedItemPropertySemanticDataSet>(stream, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Failure(
                ItemPropertySemanticImportDiagnosticCodes.JsonMalformed,
                null,
                $"Reviewed item-property semantic JSON could not be parsed: {exception.Message}");
        }

        if (dataSet is null)
        {
            return Failure(
                ItemPropertySemanticImportDiagnosticCodes.SchemaUnsupported,
                null,
                "Reviewed item-property semantic JSON root must be an object.");
        }

        var sourceRecordsRead = dataSet.Descriptors?.Count ?? 0;
        var diagnostics = ValidateDataSet(dataSet, knownStats);
        if (diagnostics.Count > 0)
        {
            return new ImportResult<ItemPropertySemanticDescriptor>
            {
                Diagnostics = diagnostics,
                SourceRecordsRead = sourceRecordsRead,
                RecordsSkipped = sourceRecordsRead,
            };
        }

        var records = dataSet.Descriptors!.ToArray();
        return new ImportResult<ItemPropertySemanticDescriptor>
        {
            ImportedRecords = records,
            SourceRecordsRead = records.Length,
            RecordsImported = records.Length,
        };
    }

    private static IReadOnlyList<ImportDiagnostic> ValidateDataSet(
        ReviewedItemPropertySemanticDataSet dataSet,
        IReadOnlyCollection<StatDefinition>? knownStats)
    {
        var diagnostics = new List<ImportDiagnostic>();
        if (dataSet.SchemaVersion != SupportedSchemaVersion)
        {
            diagnostics.Add(Diagnostic(
                ItemPropertySemanticImportDiagnosticCodes.SchemaUnsupported,
                "schemaVersion",
                $"Reviewed item-property semantic SchemaVersion must be {SupportedSchemaVersion}."));
        }

        var reviewVersion = dataSet.ReviewVersion?.Trim();
        if (string.IsNullOrWhiteSpace(reviewVersion))
        {
            diagnostics.Add(Diagnostic(
                ItemPropertySemanticImportDiagnosticCodes.SchemaUnsupported,
                "reviewVersion",
                "Reviewed item-property semantic ReviewVersion is required."));
        }

        if (dataSet.Descriptors is null || dataSet.Descriptors.Count == 0)
        {
            diagnostics.Add(Diagnostic(
                ItemPropertySemanticImportDiagnosticCodes.SchemaUnsupported,
                "descriptors",
                "Reviewed item-property semantic Descriptors must contain at least one record."));
            return diagnostics;
        }

        var validation = GameDataPackageValidator.ValidateItemPropertySemantics(
            dataSet.Descriptors,
            knownStats);
        diagnostics.AddRange(validation.Errors.Select(error => Diagnostic(
            ItemPropertySemanticImportDiagnosticCodes.ValidationFailed,
            error.Path,
            $"{error.Code}: {error.Message}")));

        if (!string.IsNullOrWhiteSpace(reviewVersion))
        {
            for (var descriptorIndex = 0; descriptorIndex < dataSet.Descriptors.Count; descriptorIndex++)
            {
                var descriptor = dataSet.Descriptors[descriptorIndex];
                if (descriptor?.Evidence is null)
                {
                    continue;
                }

                for (var evidenceIndex = 0; evidenceIndex < descriptor.Evidence.Count; evidenceIndex++)
                {
                    var evidence = descriptor.Evidence[evidenceIndex];
                    if (evidence is not null &&
                        !string.IsNullOrWhiteSpace(evidence.ReviewVersion) &&
                        !string.Equals(evidence.ReviewVersion.Trim(), reviewVersion, StringComparison.Ordinal))
                    {
                        var path = $"descriptors[{descriptorIndex}].evidence[{evidenceIndex}].reviewVersion";
                        diagnostics.Add(Diagnostic(
                            ItemPropertySemanticImportDiagnosticCodes.ReviewVersionMismatch,
                            path,
                            $"Evidence ReviewVersion must match dataset ReviewVersion '{reviewVersion}'."));
                    }
                }
            }
        }

        return diagnostics;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = GameDataPackageJson.CreateSerializerOptions();
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        return options;
    }

    private static ImportResult<ItemPropertySemanticDescriptor> Failure(
        string code,
        string? sourceRecordId,
        string message)
    {
        return new ImportResult<ItemPropertySemanticDescriptor>
        {
            Diagnostics = [Diagnostic(code, sourceRecordId, message)],
        };
    }

    private static ImportDiagnostic Diagnostic(string code, string? sourceRecordId, string message)
    {
        return new ImportDiagnostic(code, ImportDiagnosticSeverity.Error, sourceRecordId, message);
    }
}

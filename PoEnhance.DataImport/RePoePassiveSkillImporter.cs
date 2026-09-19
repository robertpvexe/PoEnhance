using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoePassiveSkillImporter
{
    public ImportResult<PassiveSkillIdentity> Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return Failure(RePoeImportDiagnosticCodes.FileNotFound,
                $"RePoE passive skill tree file was not found: {filePath}");
        }

        using var stream = File.OpenRead(filePath);
        return Import(stream);
    }

    public ImportResult<PassiveSkillIdentity> Import(Stream stream)
    {
        try
        {
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("passives", out var passives) ||
                passives.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    RePoeImportDiagnosticCodes.SchemaUnsupported,
                    "RePoE passive skill tree requires an object-valued 'passives' property.");
            }

            var records = new List<PassiveSkillIdentity>();
            var diagnostics = new List<ImportDiagnostic>();
            var seenHashes = new HashSet<int>();
            var read = 0;
            var skipped = 0;
            foreach (var property in passives.EnumerateObject())
            {
                read++;
                var value = property.Value;
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(nameElement.GetString()) ||
                    !value.TryGetProperty("hash", out var hashElement) ||
                    !hashElement.TryGetInt32(out var passiveHash) ||
                    passiveHash < 0 ||
                    !seenHashes.Add(passiveHash))
                {
                    skipped++;
                    diagnostics.Add(new ImportDiagnostic(
                        RePoeImportDiagnosticCodes.PassiveSkillRecordMalformed,
                        ImportDiagnosticSeverity.Warning,
                        property.Name,
                        "Passive skill requires a unique non-negative hash and canonical English name."));
                    continue;
                }

                var internalId = value.TryGetProperty("id", out var idElement) &&
                    idElement.ValueKind == JsonValueKind.String
                    ? TrimToNull(idElement.GetString())
                    : null;
                records.Add(new PassiveSkillIdentity
                {
                    CanonicalName = nameElement.GetString()!.Trim(),
                    PassiveHash = passiveHash,
                    InternalId = internalId,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = RePoeBaseItemImporter.SourceId,
                            ExternalId = property.Name,
                            ExternalUri = "data/passive_skill_trees/Default.json",
                        },
                    ],
                });
            }

            var ordered = records
                .OrderBy(record => record.CanonicalName, StringComparer.Ordinal)
                .ThenBy(record => record.PassiveHash)
                .ToArray();
            return new ImportResult<PassiveSkillIdentity>
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
                $"RePoE passive skill tree could not be parsed as JSON: {exception.Message}");
        }
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static ImportResult<PassiveSkillIdentity> Failure(string code, string message) => new()
    {
        Diagnostics = [new ImportDiagnostic(code, ImportDiagnosticSeverity.Error, null, message)],
    };
}

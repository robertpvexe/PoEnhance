using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeModsByBaseImporter
{
    public RePoeModsByBaseImportResult Import(
        string filePath,
        string baseItemsPath,
        string modsPath,
        IReadOnlyCollection<ItemBaseRecord> importedBases,
        IReadOnlyCollection<ModifierDefinition> importedModifiers)
    {
        ArgumentNullException.ThrowIfNull(importedBases);
        ArgumentNullException.ThrowIfNull(importedModifiers);

        foreach (var input in new[]
                 {
                     (Path: filePath, Label: "mods_by_base.json"),
                     (Path: baseItemsPath, Label: "base_items.json"),
                     (Path: modsPath, Label: "mods.json"),
                 })
        {
            if (!File.Exists(input.Path))
            {
                return Failure(
                    RePoeImportDiagnosticCodes.FileNotFound,
                    $"RePoE {input.Label} file was not found: {input.Path}");
            }
        }

        try
        {
            var rawBaseIds = ReadRawObjectIds(baseItemsPath, "base_items.json");
            var importedBaseIds = importedBases
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .Select(item => item.Id!.Trim())
                .ToHashSet(StringComparer.Ordinal);
            var importedModifierIds = importedModifiers
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .Select(item => item.Id!.Trim())
                .ToHashSet(StringComparer.Ordinal);
            var rawModifiers = ReadRawModifiers(modsPath, importedModifierIds);

            using var document = JsonDocument.Parse(File.ReadAllBytes(filePath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    RePoeImportDiagnosticCodes.SchemaUnsupported,
                    "RePoE mods_by_base.json root must be an object.");
            }

            return ImportRoot(
                document.RootElement,
                rawBaseIds,
                importedBaseIds,
                rawModifiers);
        }
        catch (JsonException exception)
        {
            return Failure(
                RePoeImportDiagnosticCodes.JsonMalformed,
                $"RePoE base/mod eligibility inputs could not be parsed as JSON: {exception.Message}");
        }
    }

    private static RePoeModsByBaseImportResult ImportRoot(
        JsonElement root,
        ISet<string> rawBaseIds,
        ISet<string> importedBaseIds,
        IReadOnlyDictionary<string, RawModifier> rawModifiers)
    {
        var diagnostics = new List<ImportDiagnostic>();
        var groups = new List<BaseModifierSourceEvidenceGroup>();
        var seenBaseEntries = new HashSet<string>(StringComparer.Ordinal);
        var seenRelationships = new HashSet<(string BaseId, string ModifierId)>();
        var sourceGenerationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var sourceBucketCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var sourceGroupsRead = 0;
        var groupsImported = 0;
        var specialEntries = 0;
        var sourceBaseEntriesRead = 0;
        var baseEntriesImported = 0;
        var baseEntriesSkipped = 0;
        var duplicateBaseEntries = 0;
        var sourceRelationshipsRead = 0;
        var relationshipsImported = 0;
        var duplicateRelationships = 0;
        var unknownBaseReferences = 0;
        var relationshipsUnavailableBases = 0;
        var statlessRelationships = 0;
        var otherUnavailableRelationships = 0;
        var unknownModifierRelationships = 0;
        var malformedRelationships = 0;

        foreach (var itemClass in root.EnumerateObject())
        {
            if (itemClass.Value.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Error(
                    RePoeImportDiagnosticCodes.ModsByBaseRecordMalformed,
                    itemClass.Name,
                    "RePoE mods_by_base item-class entry must be an object."));
                continue;
            }

            foreach (var tagSet in itemClass.Value.EnumerateObject())
            {
                var sourcePath = SourcePath(itemClass.Name, tagSet.Name);
                if (tagSet.Value.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Error(
                        RePoeImportDiagnosticCodes.ModsByBaseRecordMalformed,
                        sourcePath,
                        "RePoE mods_by_base tag-set entry must be an object."));
                    continue;
                }

                var hasBases = tagSet.Value.TryGetProperty("bases", out var sourceBases);
                var hasMods = tagSet.Value.TryGetProperty("mods", out var sourceMods);
                if (!hasBases && !hasMods)
                {
                    specialEntries++;
                    diagnostics.Add(new ImportDiagnostic(
                        RePoeImportDiagnosticCodes.ModsByBaseSpecialEntryNotModeled,
                        ImportDiagnosticSeverity.Information,
                        sourcePath,
                        "RePoE mods_by_base entry is not a base/tag-set relationship and was intentionally not modeled."));
                    continue;
                }

                sourceGroupsRead++;
                if (!hasBases || sourceBases.ValueKind != JsonValueKind.Array ||
                    !hasMods || sourceMods.ValueKind != JsonValueKind.Object ||
                    !TryReadConditionalModifiers(tagSet.Value, out var conditionalModifiers))
                {
                    diagnostics.Add(Error(
                        RePoeImportDiagnosticCodes.ModsByBaseRecordMalformed,
                        sourcePath,
                        "RePoE mods_by_base tag-set entry has malformed bases, mods, or conditional_mods data."));
                    continue;
                }

                var allSourceBases = new List<string>();
                var representedBases = new List<string>();
                foreach (var sourceBase in sourceBases.EnumerateArray())
                {
                    sourceBaseEntriesRead++;
                    if (sourceBase.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(sourceBase.GetString()))
                    {
                        baseEntriesSkipped++;
                        unknownBaseReferences++;
                        diagnostics.Add(Error(
                            RePoeImportDiagnosticCodes.ModsByBaseUnknownBase,
                            sourcePath,
                            "RePoE mods_by_base contains an unusable base id."));
                        continue;
                    }

                    var baseId = sourceBase.GetString()!.Trim();
                    allSourceBases.Add(baseId);
                    if (!seenBaseEntries.Add(baseId))
                    {
                        duplicateBaseEntries++;
                        diagnostics.Add(Error(
                            RePoeImportDiagnosticCodes.ModsByBaseDuplicateBase,
                            baseId,
                            "RePoE mods_by_base contains the base in more than one tag-set entry."));
                    }

                    if (importedBaseIds.Contains(baseId))
                    {
                        baseEntriesImported++;
                        representedBases.Add(baseId);
                    }
                    else if (rawBaseIds.Contains(baseId))
                    {
                        baseEntriesSkipped++;
                        diagnostics.Add(new ImportDiagnostic(
                            RePoeImportDiagnosticCodes.ModsByBaseBaseUnavailable,
                            ImportDiagnosticSeverity.Warning,
                            baseId,
                            "RePoE mods_by_base references a source base intentionally unavailable under the existing base-item importer rules."));
                    }
                    else
                    {
                        baseEntriesSkipped++;
                        unknownBaseReferences++;
                        diagnostics.Add(Error(
                            RePoeImportDiagnosticCodes.ModsByBaseUnknownBase,
                            baseId,
                            "RePoE mods_by_base references a base absent from base_items.json."));
                    }
                }

                var rawEvidence = ReadEvidenceEntries(
                    sourceMods,
                    conditionalModifiers,
                    sourcePath,
                    diagnostics);
                var rawModifierIds = rawEvidence.Select(item => item.ModifierId).ToHashSet(StringComparer.Ordinal);
                foreach (var conditionalModifier in conditionalModifiers)
                {
                    if (!rawModifierIds.Contains(conditionalModifier))
                    {
                        diagnostics.Add(Error(
                            RePoeImportDiagnosticCodes.ModsByBaseConditionalModifierUnknown,
                            conditionalModifier,
                            "RePoE mods_by_base conditional_mods references a modifier absent from the tag-set mods data."));
                    }
                }

                var representedEvidence = new List<BaseModifierSourceEvidenceEntry>();
                foreach (var evidence in rawEvidence)
                {
                    var relationshipMultiplicity = allSourceBases.Count;
                    sourceRelationshipsRead += relationshipMultiplicity;
                    AddCount(sourceBucketCounts, evidence.SourceGenerationBucket, relationshipMultiplicity);
                    var modifier = rawModifiers.GetValueOrDefault(evidence.ModifierId);
                    AddCount(
                        sourceGenerationCounts,
                        modifier?.SourceGenerationType ?? "<missing>",
                        relationshipMultiplicity);

                    if (!evidence.IsUsable)
                    {
                        malformedRelationships += relationshipMultiplicity;
                        continue;
                    }

                    var modifierRepresented = modifier?.Disposition == ModifierDisposition.Imported;
                    var includeEvidence = false;
                    foreach (var baseId in allSourceBases)
                    {
                        if (!importedBaseIds.Contains(baseId))
                        {
                            relationshipsUnavailableBases++;
                            continue;
                        }

                        switch (modifier?.Disposition)
                        {
                            case ModifierDisposition.Imported:
                                if (!seenRelationships.Add((baseId, evidence.ModifierId)))
                                {
                                    duplicateRelationships++;
                                    diagnostics.Add(Error(
                                        RePoeImportDiagnosticCodes.ModsByBaseDuplicateRelationship,
                                        $"{baseId}|{evidence.ModifierId}",
                                        "RePoE mods_by_base contains a duplicate base/modifier relationship."));
                                    continue;
                                }

                                relationshipsImported++;
                                includeEvidence = true;
                                break;
                            case ModifierDisposition.Statless:
                                statlessRelationships++;
                                break;
                            case ModifierDisposition.OtherUnavailable:
                                otherUnavailableRelationships++;
                                break;
                            default:
                                unknownModifierRelationships++;
                                break;
                        }
                    }

                    if (modifierRepresented && includeEvidence)
                    {
                        representedEvidence.Add(new BaseModifierSourceEvidenceEntry
                        {
                            ModifierId = evidence.ModifierId,
                            ReportedWeight = evidence.Weight,
                            IsConditional = evidence.IsConditional,
                            SourceGenerationBucket = evidence.SourceGenerationBucket,
                        });
                    }
                    else if (modifier?.Disposition == ModifierDisposition.Statless && representedBases.Count > 0)
                    {
                        diagnostics.Add(new ImportDiagnostic(
                            RePoeImportDiagnosticCodes.ModsByBaseStatlessModifierUnavailable,
                            ImportDiagnosticSeverity.Warning,
                            evidence.ModifierId,
                            $"RePoE mods_by_base evidence references a statless modifier omitted by the existing importer for {representedBases.Count} base entries."));
                    }
                    else if (modifier?.Disposition == ModifierDisposition.OtherUnavailable && representedBases.Count > 0)
                    {
                        diagnostics.Add(Error(
                            RePoeImportDiagnosticCodes.ModsByBaseOtherModifierUnavailable,
                            evidence.ModifierId,
                            "RePoE mods_by_base references a modifier skipped for a reason other than the intentional statless rule."));
                    }
                    else if (modifier is null && representedBases.Count > 0)
                    {
                        diagnostics.Add(Error(
                            RePoeImportDiagnosticCodes.ModsByBaseUnknownModifier,
                            evidence.ModifierId,
                            "RePoE mods_by_base references a modifier absent from mods.json."));
                    }
                }

                if (representedBases.Count > 0 && representedEvidence.Count > 0)
                {
                    groupsImported++;
                    groups.Add(new BaseModifierSourceEvidenceGroup
                    {
                        BaseItemIds = representedBases.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                        Modifiers = representedEvidence
                            .OrderBy(item => item.ModifierId, StringComparer.Ordinal)
                            .ThenBy(item => item.SourceGenerationBucket, StringComparer.Ordinal)
                            .ToArray(),
                        Sources = [SourceReference(sourcePath)],
                    });
                }
            }
        }

        var audit = new RePoeModsByBaseImportAudit
        {
            SourceGroupsRead = sourceGroupsRead,
            GroupsImported = groupsImported,
            SpecialSourceEntriesNotModeled = specialEntries,
            SourceBaseEntriesRead = sourceBaseEntriesRead,
            BaseEntriesImported = baseEntriesImported,
            BaseEntriesSkipped = baseEntriesSkipped,
            DuplicateBaseEntries = duplicateBaseEntries,
            SourceRelationshipsRead = sourceRelationshipsRead,
            RelationshipsImported = relationshipsImported,
            DuplicateRelationships = duplicateRelationships,
            UnknownBaseReferences = unknownBaseReferences,
            RelationshipsUnavailableBases = relationshipsUnavailableBases,
            RelationshipsUnavailableStatlessModifiers = statlessRelationships,
            RelationshipsUnavailableOtherModifiers = otherUnavailableRelationships,
            UnknownModifierRelationships = unknownModifierRelationships,
            MalformedRelationships = malformedRelationships,
            SourceGenerationRelationshipCounts = sourceGenerationCounts,
            SourceGenerationBucketRelationshipCounts = sourceBucketCounts,
        };
        var evidenceCatalog = new BaseModifierSourceEvidence
        {
            Semantics = BaseModifierEvidenceSemantics.PositiveAndContextualOnly,
            Coverage = BaseModifierEvidenceCoverage.Partial,
            SourceBaseEntriesRead = sourceBaseEntriesRead,
            BaseEntriesRepresented = baseEntriesImported,
            BaseEntriesUnavailable = baseEntriesSkipped,
            SourceRelationshipsRead = sourceRelationshipsRead,
            RelationshipsRepresented = relationshipsImported,
            RelationshipsUnavailableBases = relationshipsUnavailableBases,
            RelationshipsUnavailableStatlessModifiers = statlessRelationships,
            RelationshipsUnavailableOtherModifiers = otherUnavailableRelationships,
            RelationshipsUnresolved = audit.UnresolvedRelationships,
            SpecialSourceEntriesNotModeled = specialEntries,
            Groups = groups
                .OrderBy(group => group.BaseItemIds.FirstOrDefault(), StringComparer.Ordinal)
                .ThenBy(group => group.Sources[0].ExternalId, StringComparer.Ordinal)
                .ToArray(),
            Sources = [SourceReference("mods_by_base.json")],
        };

        return new RePoeModsByBaseImportResult
        {
            Evidence = evidenceCatalog,
            Diagnostics = diagnostics,
            Audit = audit,
        };
    }

    private static IReadOnlyList<RawEvidence> ReadEvidenceEntries(
        JsonElement sourceMods,
        ISet<string> conditionalModifiers,
        string sourcePath,
        List<ImportDiagnostic> diagnostics)
    {
        var evidence = new List<RawEvidence>();
        var seenModifierIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var generation in sourceMods.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(generation.Name) || generation.Value.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Error(
                    RePoeImportDiagnosticCodes.ModsByBaseRecordMalformed,
                    sourcePath,
                    "RePoE mods_by_base generation bucket is malformed."));
                continue;
            }

            foreach (var modifierType in generation.Value.EnumerateObject())
            {
                if (modifierType.Value.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Error(
                        RePoeImportDiagnosticCodes.ModsByBaseRecordMalformed,
                        sourcePath,
                        "RePoE mods_by_base modifier-type bucket is malformed."));
                    continue;
                }

                foreach (var modifier in modifierType.Value.EnumerateObject())
                {
                    var id = modifier.Name.Trim();
                    var weight = 0;
                    var usable = !string.IsNullOrWhiteSpace(id) &&
                        modifier.Value.ValueKind == JsonValueKind.Number &&
                        modifier.Value.TryGetInt32(out weight) &&
                        weight >= 0 &&
                        seenModifierIds.Add(id);
                    if (!usable)
                    {
                        diagnostics.Add(Error(
                            RePoeImportDiagnosticCodes.ModsByBaseRecordMalformed,
                            string.IsNullOrWhiteSpace(id) ? sourcePath : id,
                            "RePoE mods_by_base modifier relationship is malformed or duplicated inside its tag set."));
                    }

                    evidence.Add(new RawEvidence(
                        id,
                        usable ? weight : 0,
                        conditionalModifiers.Contains(id),
                        generation.Name,
                        usable));
                }
            }
        }

        return evidence;
    }

    private static bool TryReadConditionalModifiers(JsonElement tagSet, out ISet<string> modifiers)
    {
        modifiers = new HashSet<string>(StringComparer.Ordinal);
        if (!tagSet.TryGetProperty("conditional_mods", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()) ||
                !modifiers.Add(item.GetString()!.Trim()))
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<string> ReadRawObjectIds(string path, string label)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"RePoE {label} root must be an object.");
        }

        return document.RootElement.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, RawModifier> ReadRawModifiers(
        string path,
        ISet<string> importedModifierIds)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("RePoE mods.json root must be an object.");
        }

        var modifiers = new Dictionary<string, RawModifier>(StringComparer.Ordinal);
        foreach (var sourceRecord in document.RootElement.EnumerateObject())
        {
            var source = sourceRecord.Value;
            var sourceGenerationType = source.ValueKind == JsonValueKind.Object &&
                source.TryGetProperty("generation_type", out var generation) &&
                generation.ValueKind == JsonValueKind.String
                ? generation.GetString()?.Trim() ?? "<unknown>"
                : "<unknown>";
            ModifierDisposition disposition;
            if (importedModifierIds.Contains(sourceRecord.Name))
            {
                disposition = ModifierDisposition.Imported;
            }
            else if (source.ValueKind == JsonValueKind.Object &&
                     source.TryGetProperty("stats", out var stats) &&
                     stats.ValueKind == JsonValueKind.Array &&
                     stats.GetArrayLength() == 0)
            {
                disposition = ModifierDisposition.Statless;
            }
            else
            {
                disposition = ModifierDisposition.OtherUnavailable;
            }

            modifiers.TryAdd(sourceRecord.Name, new RawModifier(disposition, sourceGenerationType));
        }

        return modifiers;
    }

    private static string SourcePath(string itemClassName, string tagSet) =>
        $"mods_by_base.json#/{EscapeJsonPointer(itemClassName)}/{EscapeJsonPointer(tagSet)}";

    private static string EscapeJsonPointer(string value) => value.Replace("~", "~0", StringComparison.Ordinal)
        .Replace("/", "~1", StringComparison.Ordinal);

    private static GameDataSourceReference SourceReference(string externalId) => new()
    {
        SourceId = RePoeBaseItemImporter.SourceId,
        ExternalId = externalId,
    };

    private static void AddCount(IDictionary<string, int> counts, string key, int value)
    {
        counts.TryGetValue(key, out var current);
        counts[key] = current + value;
    }

    private static RePoeModsByBaseImportResult Failure(string code, string message) => new()
    {
        Diagnostics = [new ImportDiagnostic(code, ImportDiagnosticSeverity.Error, null, message)],
    };

    private static ImportDiagnostic Error(string code, string? id, string message) =>
        new(code, ImportDiagnosticSeverity.Error, id, message);

    private enum ModifierDisposition
    {
        Imported,
        Statless,
        OtherUnavailable,
    }

    private sealed record RawModifier(ModifierDisposition Disposition, string SourceGenerationType);

    private sealed record RawEvidence(
        string ModifierId,
        int Weight,
        bool IsConditional,
        string SourceGenerationBucket,
        bool IsUsable);
}

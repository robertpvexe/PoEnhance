using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class PoBFoulbornRelationshipImporter
{
    public PoBFoulbornRelationshipImportResult Import(
        string filePath,
        string sourcePath,
        string repositoryUri,
        string tag,
        string commitSha,
        UniqueItemCatalog uniqueItems,
        IReadOnlyList<ModifierDefinition> modifiers)
    {
        ArgumentNullException.ThrowIfNull(uniqueItems);
        ArgumentNullException.ThrowIfNull(modifiers);

        if (!File.Exists(filePath))
        {
            return Failure(
                RePoeImportDiagnosticCodes.PoBFoulbornFileNotFound,
                $"Path of Building Foulborn relationship input was not found: {filePath}");
        }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    RePoeImportDiagnosticCodes.PoBFoulbornSchemaUnsupported,
                    "Path of Building Foulborn relationship input must be an object keyed by item name.");
            }

            return ImportRelationships(
                document.RootElement,
                bytes,
                sourcePath,
                repositoryUri,
                tag,
                commitSha,
                uniqueItems,
                modifiers);
        }
        catch (JsonException exception)
        {
            return Failure(
                RePoeImportDiagnosticCodes.PoBFoulbornJsonMalformed,
                $"Path of Building Foulborn relationship input is invalid JSONC: {exception.Message}");
        }
    }

    private static PoBFoulbornRelationshipImportResult ImportRelationships(
        JsonElement root,
        byte[] sourceBytes,
        string sourcePath,
        string repositoryUri,
        string tag,
        string commitSha,
        UniqueItemCatalog uniqueItems,
        IReadOnlyList<ModifierDefinition> modifiers)
    {
        var diagnostics = new List<ImportDiagnostic>();
        var relationships = new List<UniqueFoulbornModifierRelationship>();
        var exactTriples = new HashSet<string>(StringComparer.Ordinal);
        var itemAndNormalTargets = new Dictionary<string, string>(StringComparer.Ordinal);
        var modifierIds = modifiers
            .Where(modifier => !string.IsNullOrWhiteSpace(modifier.Id))
            .Select(modifier => modifier.Id!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fileSha256 = Sha256(sourceBytes);
        var normalizedSourcePath = sourcePath.Trim().Replace('\\', '/');
        var sourceObservationId = StableId(
            "pob-foulborn-source",
            commitSha.Trim().ToLowerInvariant(),
            normalizedSourcePath,
            fileSha256);
        var itemCount = 0;
        var relationshipCount = 0;
        var linkedCount = 0;
        var unsupportedCount = 0;

        foreach (var itemProperty in root.EnumerateObject())
        {
            itemCount++;
            var itemName = itemProperty.Name.Trim();
            if (itemName.Length == 0 || itemProperty.Value.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.PoBFoulbornSchemaUnsupported,
                    ImportDiagnosticSeverity.Error,
                    itemProperty.Name,
                    "Each Foulborn item entry must have a non-empty name and an object of modifier relationships."));
                continue;
            }

            var exactIdentities = uniqueItems.Items
                .Where(identity =>
                    identity.Kind == UniqueItemKind.Ordinary &&
                    string.Equals(identity.CanonicalName, itemName, StringComparison.Ordinal))
                .ToArray();
            var normalizedItemKey = UniqueSourceIdentityNormalizer.NormalizeKey(itemName);
            var identities = exactIdentities.Length > 0
                ? exactIdentities
                : uniqueItems.Items
                    .Where(identity =>
                        identity.Kind == UniqueItemKind.Ordinary &&
                        !string.IsNullOrWhiteSpace(identity.CanonicalName) &&
                        string.Equals(
                            identity.CanonicalIdentityKey ??
                                $"ordinary|{UniqueSourceIdentityNormalizer.NormalizeKey(identity.CanonicalName)}",
                            $"ordinary|{normalizedItemKey}",
                            StringComparison.Ordinal))
                    .ToArray();
            var identityNormalizationRule = exactIdentities.Length > 0
                ? UniqueSourceIdentityNormalizer.ExactRule
                : identities.Length == 1
                    ? UniqueSourceIdentityNormalizer.CanonicalRule
                    : "unresolved-source-identity-v1";

            foreach (var relationProperty in itemProperty.Value.EnumerateObject())
            {
                relationshipCount++;
                var normalModifierId = relationProperty.Name.Trim();
                var foulbornModifierId = relationProperty.Value.ValueKind == JsonValueKind.String
                    ? relationProperty.Value.GetString()?.Trim()
                    : null;
                if (normalModifierId.Length == 0 || string.IsNullOrWhiteSpace(foulbornModifierId))
                {
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.PoBFoulbornSchemaUnsupported,
                        ImportDiagnosticSeverity.Error,
                        itemName,
                        "Each Foulborn relationship must map a non-empty normal modifier id to a string replacement modifier id."));
                    continue;
                }

                var tripleKey = string.Join('\u001f', itemName, normalModifierId, foulbornModifierId);
                if (!exactTriples.Add(tripleKey))
                {
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.PoBFoulbornDuplicateRelationship,
                        ImportDiagnosticSeverity.Error,
                        itemName,
                        $"Duplicate Foulborn relationship '{normalModifierId}' to '{foulbornModifierId}'."));
                    continue;
                }

                var itemAndNormalKey = string.Join('\u001f', itemName, normalModifierId);
                if (itemAndNormalTargets.TryGetValue(itemAndNormalKey, out var existingTarget) &&
                    !string.Equals(existingTarget, foulbornModifierId, StringComparison.Ordinal))
                {
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.PoBFoulbornConflictingRelationship,
                        ImportDiagnosticSeverity.Error,
                        itemName,
                        $"Normal modifier '{normalModifierId}' has conflicting Foulborn targets '{existingTarget}' and '{foulbornModifierId}'."));
                }
                else
                {
                    itemAndNormalTargets[itemAndNormalKey] = foulbornModifierId;
                }

                var diagnosticCode = default(string);
                var diagnostic = default(string);
                if (identities.Length == 0)
                {
                    diagnosticCode = "FOULBORN_UNIQUE_IDENTITY_NOT_FOUND";
                    diagnostic = "The source item name has no exact ordinary Unique identity in the imported catalog.";
                }
                else if (identities.Length > 1)
                {
                    diagnosticCode = "FOULBORN_UNIQUE_IDENTITY_AMBIGUOUS";
                    diagnostic = "The source item name matches multiple ordinary Unique identities.";
                }
                else if (!modifierIds.Contains(normalModifierId))
                {
                    diagnosticCode = "FOULBORN_NORMAL_MODIFIER_NOT_FOUND";
                    diagnostic = "The normal-side modifier id does not exist in the imported RePoE modifiers.";
                }
                else if (!modifierIds.Contains(foulbornModifierId))
                {
                    diagnosticCode = "FOULBORN_REPLACEMENT_MODIFIER_NOT_FOUND";
                    diagnostic = "The Foulborn-side modifier id does not exist in the imported RePoE modifiers.";
                }

                var identity = identities.Length == 1 ? identities[0] : null;
                var normalBlockIds = identity?.Versions
                    .SelectMany(version => version.ModifierBlocks)
                    .Where(block => block.MechanicalMapping.ModifierIds.Contains(
                        normalModifierId,
                        StringComparer.OrdinalIgnoreCase))
                    .Select(block => block.Id?.Trim())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray() ?? [];
                var status = diagnosticCode is null
                    ? UniqueFoulbornModifierRelationshipStatus.Exact
                    : UniqueFoulbornModifierRelationshipStatus.Unsupported;
                if (status == UniqueFoulbornModifierRelationshipStatus.Exact)
                {
                    linkedCount++;
                }
                else
                {
                    unsupportedCount++;
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.PoBFoulbornRelationshipUnsupported,
                        ImportDiagnosticSeverity.Warning,
                        itemName,
                        $"{diagnosticCode}: {diagnostic}"));
                }

                relationships.Add(new UniqueFoulbornModifierRelationship
                {
                    Id = StableId(
                        "foulborn-relationship",
                        itemName,
                        normalModifierId,
                        foulbornModifierId,
                        sourceObservationId),
                    ItemName = itemName,
                    CanonicalItemName = identity?.CanonicalName,
                    CanonicalIdentityKey = identity?.CanonicalIdentityKey ??
                        (identity?.CanonicalName is null
                            ? $"ordinary|{normalizedItemKey}"
                            : $"ordinary|{UniqueSourceIdentityNormalizer.NormalizeKey(identity.CanonicalName)}"),
                    IdentityNormalizationRule = identityNormalizationRule,
                    IdentityLinkageEvidence = identity is null
                        ? "No collision-free ordinary identity linkage was proven from the pinned PoB relationship map and Unique catalog."
                        : exactIdentities.Length == 1
                            ? "Exact item display text links the pinned PoB relationship map to one ordinary Unique identity."
                            : "The relationship map and Unique catalog are from the same pinned PoB commit; their canonical Unicode/diacritic/punctuation key links to exactly one ordinary identity without a collision.",
                    CurrentHistoryDecisionReason =
                        "The pinned Foulborn replacement map is linked only to the ordinary identity's explicit current-role observations; historical observations are excluded.",
                    UniqueItemId = identity?.Id,
                    NormalModifierId = normalModifierId,
                    FoulbornModifierId = foulbornModifierId,
                    NormalModifierBlockIds = normalBlockIds,
                    AppliesToRole = UniqueItemVersionRole.Current,
                    SourceObservationId = sourceObservationId,
                    Status = status,
                    DiagnosticCode = diagnosticCode,
                    Diagnostic = diagnostic,
                });
            }
        }

        return new PoBFoulbornRelationshipImportResult
        {
            SourceObservation = new UniqueFoulbornRelationshipSourceObservation
            {
                Id = sourceObservationId,
                ManifestSourceId = PoBUniqueCatalogImporter.SourceId,
                RepositoryUri = repositoryUri.Trim(),
                Tag = tag.Trim(),
                CommitSha = commitSha.Trim().ToLowerInvariant(),
                SourcePath = normalizedSourcePath,
                SourceFileSha256 = fileSha256,
            },
            Relationships = relationships
                .OrderBy(relationship => relationship.ItemName, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.NormalModifierId, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.FoulbornModifierId, StringComparer.Ordinal)
                .ToArray(),
            Diagnostics = diagnostics,
            ItemRecordsRead = itemCount,
            RelationshipsRead = relationshipCount,
            RelationshipsLinked = linkedCount,
            RelationshipsUnsupported = unsupportedCount,
        };
    }

    private static string StableId(string prefix, params string[] parts)
    {
        return $"{prefix}:{Sha256(string.Join('\u001f', parts))[..24]}";
    }

    private static string Sha256(string value) => Sha256(Encoding.UTF8.GetBytes(value));

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static ImportDiagnostic Diagnostic(
        string code,
        ImportDiagnosticSeverity severity,
        string? sourceRecordId,
        string message) => new(code, severity, sourceRecordId, message);

    private static PoBFoulbornRelationshipImportResult Failure(string code, string message) => new()
    {
        Diagnostics = [Diagnostic(code, ImportDiagnosticSeverity.Error, null, message)],
    };
}

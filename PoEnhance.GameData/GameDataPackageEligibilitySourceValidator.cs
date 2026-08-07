namespace PoEnhance.GameData;

internal static class GameDataPackageEligibilitySourceValidator
{
    public static IReadOnlyList<GameDataValidationError> Validate(
        GameDataPackage package,
        ISet<string> manifestSourceIds)
    {
        var errors = new List<GameDataValidationError>();
        var tagIds = package.Tags is null
            ? null
            : ValidateTags(package.Tags, manifestSourceIds, errors);
        var itemClassIds = package.ItemClasses is null
            ? null
            : ValidateItemClasses(package.ItemClasses, tagIds, manifestSourceIds, errors);

        ValidateCatalogReferences(package, itemClassIds, tagIds, errors);
        if (package.BaseModifierEvidence is not null)
        {
            ValidateEvidence(package, manifestSourceIds, errors);
        }

        return errors;
    }

    private static HashSet<string> ValidateTags(
        IReadOnlyList<TagDefinition> tags,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < tags.Count; index++)
        {
            var path = $"tags[{index}]";
            var tag = tags[index];
            if (tag is null)
            {
                errors.Add(Error(GameDataValidationErrorCodes.TagRequired, path, "Tag is required."));
                continue;
            }

            var id = tag.Id?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(Error(GameDataValidationErrorCodes.TagIdRequired, $"{path}.id", "Tag Id is required."));
            }
            else if (!ids.Add(id))
            {
                errors.Add(Error(GameDataValidationErrorCodes.TagIdDuplicate, $"{path}.id", $"Tag Id '{id}' is duplicated."));
            }

            ValidateRequiredSources(tag.Sources, $"{path}.sources", GameDataValidationErrorCodes.TagSourcesRequired, manifestSourceIds, errors);
        }

        return ids;
    }

    private static HashSet<string> ValidateItemClasses(
        IReadOnlyList<ItemClassDefinition> itemClasses,
        ISet<string>? knownTagIds,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < itemClasses.Count; index++)
        {
            var path = $"itemClasses[{index}]";
            var itemClass = itemClasses[index];
            if (itemClass is null)
            {
                errors.Add(Error(GameDataValidationErrorCodes.ItemClassRequired, path, "Item class is required."));
                continue;
            }

            var id = itemClass.Id?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(Error(GameDataValidationErrorCodes.ItemClassIdRequired, $"{path}.id", "Item class Id is required."));
            }
            else if (!ids.Add(id))
            {
                errors.Add(Error(GameDataValidationErrorCodes.ItemClassIdDuplicate, $"{path}.id", $"Item class Id '{id}' is duplicated."));
            }

            for (var tagIndex = 0; tagIndex < itemClass.InfluenceTagIds.Count; tagIndex++)
            {
                var tagId = itemClass.InfluenceTagIds[tagIndex]?.Trim();
                if (knownTagIds is not null && (string.IsNullOrWhiteSpace(tagId) || !knownTagIds.Contains(tagId)))
                {
                    errors.Add(Error(
                        GameDataValidationErrorCodes.ItemClassInfluenceTagUnknown,
                        $"{path}.influenceTagIds[{tagIndex}]",
                        $"Item class influence tag '{tagId}' is not declared in package tags."));
                }
            }

            ValidateRequiredSources(itemClass.Sources, $"{path}.sources", GameDataValidationErrorCodes.ItemClassSourcesRequired, manifestSourceIds, errors);
        }

        return ids;
    }

    private static void ValidateCatalogReferences(
        GameDataPackage package,
        ISet<string>? knownItemClassIds,
        ISet<string>? knownTagIds,
        List<GameDataValidationError> errors)
    {
        var itemBases = package.ItemBases ?? [];
        for (var index = 0; index < itemBases.Count; index++)
        {
            var itemBase = itemBases[index];
            if (itemBase is null)
            {
                continue;
            }

            var itemClass = itemBase.ItemClass?.Trim();
            if (knownItemClassIds is not null &&
                !string.IsNullOrWhiteSpace(itemClass) &&
                !knownItemClassIds.Contains(itemClass))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseItemClassUnknown,
                    $"itemBases[{index}].itemClass",
                    $"Item base class '{itemClass}' is not declared in package item classes."));
            }

            if (knownTagIds is not null)
            {
                ValidateKnownTags(itemBase.Tags, knownTagIds, $"itemBases[{index}].tags", GameDataValidationErrorCodes.ItemBaseTagUnknown, "Item base", errors);
            }
        }

        if (knownTagIds is null)
        {
            return;
        }

        var modifiers = package.Modifiers ?? [];
        for (var index = 0; index < modifiers.Count; index++)
        {
            var modifier = modifiers[index];
            if (modifier is null)
            {
                continue;
            }

            ValidateKnownTags(modifier.Tags, knownTagIds, $"modifiers[{index}].tags", GameDataValidationErrorCodes.ModifierTagUnknown, "Modifier", errors);
            for (var weightIndex = 0; weightIndex < modifier.SpawnWeights.Count; weightIndex++)
            {
                var tag = modifier.SpawnWeights[weightIndex]?.Tag?.Trim();
                if (!string.IsNullOrWhiteSpace(tag) && !knownTagIds.Contains(tag))
                {
                    errors.Add(Error(
                        GameDataValidationErrorCodes.ModifierSpawnWeightTagUnknown,
                        $"modifiers[{index}].spawnWeights[{weightIndex}].tag",
                        $"Modifier spawn-weight tag '{tag}' is not declared in package tags."));
                }
            }
        }
    }

    private static void ValidateKnownTags(
        IReadOnlyList<string> tags,
        ISet<string> knownTagIds,
        string path,
        string code,
        string owner,
        List<GameDataValidationError> errors)
    {
        for (var index = 0; index < tags.Count; index++)
        {
            var tag = tags[index]?.Trim();
            if (!string.IsNullOrWhiteSpace(tag) && !knownTagIds.Contains(tag))
            {
                errors.Add(Error(code, $"{path}[{index}]", $"{owner} tag '{tag}' is not declared in package tags."));
            }
        }
    }

    private static void ValidateEvidence(
        GameDataPackage package,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        var evidence = package.BaseModifierEvidence!;
        if (!Enum.IsDefined(evidence.Semantics) || evidence.Semantics == BaseModifierEvidenceSemantics.Unknown)
        {
            errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceSemanticsInvalid, "baseModifierEvidence.semantics", "Base/modifier evidence semantics must be explicit."));
        }

        if (!Enum.IsDefined(evidence.Coverage) || evidence.Coverage == BaseModifierEvidenceCoverage.Unknown)
        {
            errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceCoverageInvalid, "baseModifierEvidence.coverage", "Base/modifier evidence coverage must be explicit."));
        }

        var counts = new[]
        {
            evidence.SourceBaseEntriesRead,
            evidence.BaseEntriesRepresented,
            evidence.BaseEntriesUnavailable,
            evidence.SourceRelationshipsRead,
            evidence.RelationshipsRepresented,
            evidence.RelationshipsUnavailableBases,
            evidence.RelationshipsUnavailableStatlessModifiers,
            evidence.RelationshipsUnavailableOtherModifiers,
            evidence.RelationshipsUnresolved,
            evidence.SpecialSourceEntriesNotModeled,
        };
        if (counts.Any(count => count < 0))
        {
            errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceCountInvalid, "baseModifierEvidence", "Evidence audit counts must be non-negative."));
        }

        var knownBaseIds = (package.ItemBases ?? [])
            .Where(itemBase => itemBase is not null && !string.IsNullOrWhiteSpace(itemBase.Id))
            .Select(itemBase => itemBase.Id!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownModifierIds = (package.Modifiers ?? [])
            .Where(modifier => modifier is not null && !string.IsNullOrWhiteSpace(modifier.Id))
            .Select(modifier => modifier.Id!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRelationships = new HashSet<(string BaseId, string ModifierId)>(BaseModifierPairComparer.Instance);
        var representedRelationships = 0;

        for (var groupIndex = 0; groupIndex < evidence.Groups.Count; groupIndex++)
        {
            var path = $"baseModifierEvidence.groups[{groupIndex}]";
            var group = evidence.Groups[groupIndex];
            if (group is null)
            {
                errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceGroupRequired, path, "Evidence group is required."));
                continue;
            }

            if (group.BaseItemIds.Count == 0)
            {
                errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceGroupBasesRequired, $"{path}.baseItemIds", "Evidence group must contain at least one base."));
            }
            if (group.Modifiers.Count == 0)
            {
                errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceGroupModifiersRequired, $"{path}.modifiers", "Evidence group must contain at least one modifier."));
            }
            ValidateRequiredSources(group.Sources, $"{path}.sources", GameDataValidationErrorCodes.BaseModifierEvidenceGroupSourcesRequired, manifestSourceIds, errors);

            var usableBases = new List<string>();
            for (var baseIndex = 0; baseIndex < group.BaseItemIds.Count; baseIndex++)
            {
                var baseId = group.BaseItemIds[baseIndex]?.Trim();
                var basePath = $"{path}.baseItemIds[{baseIndex}]";
                if (string.IsNullOrWhiteSpace(baseId))
                {
                    errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceBaseIdRequired, basePath, "Evidence base Id is required."));
                    continue;
                }
                if (!knownBaseIds.Contains(baseId))
                {
                    errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceBaseIdUnknown, basePath, $"Evidence base Id '{baseId}' is unknown."));
                }
                if (!seenBases.Add(baseId))
                {
                    errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceBaseIdDuplicate, basePath, $"Evidence base Id '{baseId}' occurs in more than one group."));
                }
                usableBases.Add(baseId);
            }

            representedRelationships += group.BaseItemIds.Count * group.Modifiers.Count;
            for (var modifierIndex = 0; modifierIndex < group.Modifiers.Count; modifierIndex++)
            {
                var modifierPath = $"{path}.modifiers[{modifierIndex}]";
                var modifier = group.Modifiers[modifierIndex];
                if (modifier is null)
                {
                    errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceModifierRequired, modifierPath, "Evidence modifier is required."));
                    continue;
                }

                var modifierId = modifier.ModifierId?.Trim();
                if (string.IsNullOrWhiteSpace(modifierId))
                {
                    errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceModifierIdRequired, $"{modifierPath}.modifierId", "Evidence modifier Id is required."));
                }
                else
                {
                    if (!knownModifierIds.Contains(modifierId))
                    {
                        errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceModifierIdUnknown, $"{modifierPath}.modifierId", $"Evidence modifier Id '{modifierId}' is unknown."));
                    }
                    foreach (var baseId in usableBases)
                    {
                        if (!seenRelationships.Add((baseId, modifierId)))
                        {
                            errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceRelationshipDuplicate, modifierPath, $"Evidence relationship '{baseId}' -> '{modifierId}' is duplicated."));
                        }
                    }
                }

                if (modifier.ReportedWeight < 0)
                {
                    errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceWeightNegative, $"{modifierPath}.reportedWeight", "Reported evidence weight must be non-negative."));
                }
                if (string.IsNullOrWhiteSpace(modifier.SourceGenerationBucket))
                {
                    errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceGenerationBucketRequired, $"{modifierPath}.sourceGenerationBucket", "Source generation bucket is required."));
                }
            }
        }

        ValidateRequiredSources(evidence.Sources, "baseModifierEvidence.sources", GameDataValidationErrorCodes.BaseModifierEvidenceSourcesRequired, manifestSourceIds, errors);
        if (evidence.SourceBaseEntriesRead != evidence.BaseEntriesRepresented + evidence.BaseEntriesUnavailable ||
            evidence.RelationshipsRepresented != representedRelationships ||
            evidence.SourceRelationshipsRead != evidence.RelationshipsRepresented +
                evidence.RelationshipsUnavailableBases +
                evidence.RelationshipsUnavailableStatlessModifiers +
                evidence.RelationshipsUnresolved ||
            evidence.RelationshipsUnavailableOtherModifiers > evidence.RelationshipsUnresolved ||
            (evidence.Coverage == BaseModifierEvidenceCoverage.Complete &&
                (evidence.BaseEntriesUnavailable > 0 || evidence.RelationshipsUnavailableBases > 0 ||
                 evidence.RelationshipsUnavailableStatlessModifiers > 0 || evidence.RelationshipsUnresolved > 0)))
        {
            errors.Add(Error(GameDataValidationErrorCodes.BaseModifierEvidenceCountContradiction, "baseModifierEvidence", "Evidence audit counts or completeness claim contradict the represented records."));
        }
    }

    private static void ValidateRequiredSources(
        IReadOnlyList<GameDataSourceReference> sources,
        string path,
        string requiredCode,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        if (sources is null || sources.Count == 0)
        {
            errors.Add(Error(requiredCode, path, "Source provenance is required."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var sourcePath = $"{path}[{index}]";
            if (source is null)
            {
                errors.Add(Error(GameDataValidationErrorCodes.SourceReferenceRequired, sourcePath, "Source reference is required."));
                continue;
            }

            var sourceId = source.SourceId?.Trim();
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                errors.Add(Error(GameDataValidationErrorCodes.SourceReferenceSourceIdRequired, $"{sourcePath}.sourceId", "Source reference SourceId is required."));
            }
            else if (manifestSourceIds.Count > 0 && !manifestSourceIds.Contains(sourceId))
            {
                errors.Add(Error(GameDataValidationErrorCodes.SourceReferenceSourceIdUnknown, $"{sourcePath}.sourceId", $"SourceId '{sourceId}' is not declared by the manifest."));
            }

            var identity = $"{sourceId}\u001f{source.ExternalId?.Trim()}\u001f{source.ExternalUri?.Trim()}";
            if (!seen.Add(identity))
            {
                errors.Add(Error(GameDataValidationErrorCodes.SourceReferenceDuplicate, sourcePath, "Source reference is duplicated."));
            }
        }
    }

    private static GameDataValidationError Error(string code, string path, string message) => new(code, path, message);

    private sealed class BaseModifierPairComparer : IEqualityComparer<(string BaseId, string ModifierId)>
    {
        public static BaseModifierPairComparer Instance { get; } = new();

        public bool Equals((string BaseId, string ModifierId) x, (string BaseId, string ModifierId) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.BaseId, y.BaseId) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.ModifierId, y.ModifierId);

        public int GetHashCode((string BaseId, string ModifierId) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.BaseId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.ModifierId));
    }
}

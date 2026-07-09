namespace PoEnhance.GameData;

public static class GameDataPackageValidator
{
    public static GameDataValidationResult Validate(GameDataPackage? package)
    {
        var errors = new List<GameDataValidationError>();

        if (package is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.PackageRequired,
                "package",
                "Package is required."));
            return new GameDataValidationResult(errors);
        }

        errors.AddRange(GameDataPackageManifestValidator.Validate(package.Manifest).Errors);
        var manifestSourceIds = GetManifestSourceIds(package.Manifest);

        if (package.ItemBases is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.PackageItemBasesRequired,
                "itemBases",
                "ItemBases collection is required."));
        }
        else
        {
            ValidateItemBases(package.ItemBases, manifestSourceIds, errors);
        }

        if (package.Modifiers is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.PackageModifiersRequired,
                "modifiers",
                "Modifiers collection is required."));
        }
        else
        {
            ValidateModifiers(package.Modifiers, manifestSourceIds, errors);
        }

        return new GameDataValidationResult(errors);
    }

    private static void ValidateItemBases(
        IReadOnlyList<ItemBaseRecord> itemBases,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < itemBases.Count; index++)
        {
            var path = $"itemBases[{index}]";
            ItemBaseRecord? itemBase = itemBases[index];
            if (itemBase is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseRequired,
                    path,
                    $"ItemBases[{index}] is required."));
                continue;
            }

            var id = itemBase.Id?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseIdRequired,
                    $"{path}.id",
                    $"ItemBases[{index}].Id is required."));
            }
            else if (!ids.Add(id))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseIdDuplicate,
                    $"{path}.id",
                    $"Item base Id '{id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(itemBase.Name))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseNameRequired,
                    $"{path}.name",
                    $"ItemBases[{index}].Name is required."));
            }

            if (string.IsNullOrWhiteSpace(itemBase.ItemClass))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseItemClassRequired,
                    $"{path}.itemClass",
                    $"ItemBases[{index}].ItemClass is required."));
            }

            if (itemBase.RequiredLevel is < 0)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseRequiredLevelNegative,
                    $"{path}.requiredLevel",
                    $"ItemBases[{index}].RequiredLevel must be 0 or greater when provided."));
            }

            ValidateTags(
                itemBase.Tags,
                $"{path}.tags",
                GameDataValidationErrorCodes.ItemBaseTagRequired,
                GameDataValidationErrorCodes.ItemBaseTagDuplicate,
                "Item base tag",
                errors);

            ValidateSourceReferences(itemBase.Sources, $"{path}.sources", manifestSourceIds, errors);
        }
    }

    private static void ValidateModifiers(
        IReadOnlyList<ModifierDefinition> modifiers,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < modifiers.Count; index++)
        {
            var path = $"modifiers[{index}]";
            ModifierDefinition? modifier = modifiers[index];
            if (modifier is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierRequired,
                    path,
                    $"Modifiers[{index}] is required."));
                continue;
            }

            var id = modifier.Id?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierIdRequired,
                    $"{path}.id",
                    $"Modifiers[{index}].Id is required."));
            }
            else if (!ids.Add(id))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierIdDuplicate,
                    $"{path}.id",
                    $"Modifier Id '{id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(modifier.GroupId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierGroupIdRequired,
                    $"{path}.groupId",
                    $"Modifiers[{index}].GroupId is required."));
            }

            if (modifier.Tier is <= 0)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierTierInvalid,
                    $"{path}.tier",
                    $"Modifiers[{index}].Tier must be greater than 0 when provided."));
            }

            if (modifier.RequiredLevel is < 0)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierRequiredLevelNegative,
                    $"{path}.requiredLevel",
                    $"Modifiers[{index}].RequiredLevel must be 0 or greater when provided."));
            }

            ValidateTags(
                modifier.Tags,
                $"{path}.tags",
                GameDataValidationErrorCodes.ModifierTagRequired,
                GameDataValidationErrorCodes.ModifierTagDuplicate,
                "Modifier tag",
                errors);

            ValidateSourceReferences(modifier.Sources, $"{path}.sources", manifestSourceIds, errors);
            ValidateStats(modifier.Stats, $"{path}.stats", index, errors);
            ValidateSpawnWeights(modifier.SpawnWeights, $"{path}.spawnWeights", index, errors);
        }
    }

    private static void ValidateStats(
        IReadOnlyList<ModifierStat>? stats,
        string path,
        int modifierIndex,
        List<GameDataValidationError> errors)
    {
        if (stats is null || stats.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ModifierStatsRequired,
                path,
                $"Modifiers[{modifierIndex}].Stats must contain at least one stat."));
            return;
        }

        var indexes = new HashSet<int>();
        for (var statIndex = 0; statIndex < stats.Count; statIndex++)
        {
            var statPath = $"{path}[{statIndex}]";
            ModifierStat? stat = stats[statIndex];
            if (stat is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierStatRequired,
                    statPath,
                    $"Modifiers[{modifierIndex}].Stats[{statIndex}] is required."));
                continue;
            }

            if (stat.Index < 0)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierStatIndexNegative,
                    $"{statPath}.index",
                    $"Modifiers[{modifierIndex}].Stats[{statIndex}].Index must be 0 or greater."));
            }
            else if (!indexes.Add(stat.Index))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierStatIndexDuplicate,
                    $"{statPath}.index",
                    $"Modifiers[{modifierIndex}].Stats[{statIndex}].Index '{stat.Index}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(stat.StatId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierStatIdRequired,
                    $"{statPath}.statId",
                    $"Modifiers[{modifierIndex}].Stats[{statIndex}].StatId is required."));
            }

            if (stat.MinValue.HasValue && stat.MaxValue.HasValue && stat.MinValue.Value > stat.MaxValue.Value)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierStatRangeInvalid,
                    statPath,
                    $"Modifiers[{modifierIndex}].Stats[{statIndex}] has MinValue greater than MaxValue."));
            }
        }
    }

    private static void ValidateSpawnWeights(
        IReadOnlyList<ModifierSpawnWeight>? spawnWeights,
        string path,
        int modifierIndex,
        List<GameDataValidationError> errors)
    {
        if (spawnWeights is null)
        {
            return;
        }

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var weightIndex = 0; weightIndex < spawnWeights.Count; weightIndex++)
        {
            var spawnWeightPath = $"{path}[{weightIndex}]";
            ModifierSpawnWeight? spawnWeight = spawnWeights[weightIndex];
            if (spawnWeight is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierSpawnWeightRequired,
                    spawnWeightPath,
                    $"Modifiers[{modifierIndex}].SpawnWeights[{weightIndex}] is required."));
                continue;
            }

            var tag = spawnWeight.Tag?.Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierSpawnWeightTagRequired,
                    $"{spawnWeightPath}.tag",
                    $"Modifiers[{modifierIndex}].SpawnWeights[{weightIndex}].Tag is required."));
            }
            else if (!tags.Add(tag))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierSpawnWeightTagDuplicate,
                    $"{spawnWeightPath}.tag",
                    $"Modifiers[{modifierIndex}].SpawnWeights[{weightIndex}].Tag '{tag}' is duplicated."));
            }

            if (spawnWeight.Weight < 0)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierSpawnWeightWeightNegative,
                    $"{spawnWeightPath}.weight",
                    $"Modifiers[{modifierIndex}].SpawnWeights[{weightIndex}].Weight must be 0 or greater."));
            }
        }
    }

    private static void ValidateTags(
        IReadOnlyList<string>? tags,
        string path,
        string requiredErrorCode,
        string duplicateErrorCode,
        string label,
        List<GameDataValidationError> errors)
    {
        if (tags is null)
        {
            return;
        }

        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < tags.Count; index++)
        {
            var tag = tags[index]?.Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                errors.Add(Error(
                    requiredErrorCode,
                    $"{path}[{index}]",
                    $"{label} at {path}[{index}] is required."));
            }
            else if (!seenTags.Add(tag))
            {
                errors.Add(Error(
                    duplicateErrorCode,
                    $"{path}[{index}]",
                    $"{label} '{tag}' is duplicated."));
            }
        }
    }

    private static void ValidateSourceReferences(
        IReadOnlyList<GameDataSourceReference>? sourceReferences,
        string path,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        if (sourceReferences is null)
        {
            return;
        }

        var seenReferences = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sourceReferences.Count; index++)
        {
            var sourceReferencePath = $"{path}[{index}]";
            GameDataSourceReference? sourceReference = sourceReferences[index];
            if (sourceReference is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.SourceReferenceRequired,
                    sourceReferencePath,
                    $"Source reference at {sourceReferencePath} is required."));
                continue;
            }

            var sourceId = sourceReference.SourceId?.Trim();
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.SourceReferenceSourceIdRequired,
                    $"{sourceReferencePath}.sourceId",
                    $"Source reference at {sourceReferencePath}.SourceId is required."));
            }
            else if (!manifestSourceIds.Contains(sourceId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.SourceReferenceSourceIdUnknown,
                    $"{sourceReferencePath}.sourceId",
                    $"Source reference at {sourceReferencePath}.SourceId '{sourceId}' is not declared in the manifest."));
            }

            if (!string.IsNullOrWhiteSpace(sourceId) && !seenReferences.Add(BuildSourceReferenceKey(sourceReference)))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.SourceReferenceDuplicate,
                    sourceReferencePath,
                    $"Source reference at {sourceReferencePath} is duplicated."));
            }
        }
    }

    private static ISet<string> GetManifestSourceIds(GameDataPackageManifest? manifest)
    {
        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifest?.Sources is null)
        {
            return sourceIds;
        }

        foreach (var source in manifest.Sources)
        {
            var sourceId = source?.SourceId?.Trim();
            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                sourceIds.Add(sourceId);
            }
        }

        return sourceIds;
    }

    private static string BuildSourceReferenceKey(GameDataSourceReference sourceReference)
    {
        return string.Join(
            "\u001F",
            (sourceReference.SourceId?.Trim() ?? string.Empty).ToUpperInvariant(),
            sourceReference.ExternalId ?? string.Empty,
            sourceReference.ExternalUri ?? string.Empty);
    }

    private static GameDataValidationError Error(string code, string path, string message)
    {
        return new GameDataValidationError(code, path, message);
    }
}

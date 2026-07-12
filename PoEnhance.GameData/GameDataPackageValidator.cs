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

        HashSet<string>? knownStatIds = null;
        if (package.Stats is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.PackageStatsRequired,
                "stats",
                "Stats collection is required."));
        }
        else
        {
            knownStatIds = ValidateStatDefinitions(package.Stats, manifestSourceIds, errors);
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
            ValidateModifiers(package.Modifiers, manifestSourceIds, knownStatIds, errors);
        }

        if (package.StatTranslations is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.PackageStatTranslationsRequired,
                "statTranslations",
                "StatTranslations collection is required."));
        }
        else
        {
            ValidateStatTranslations(package.StatTranslations, manifestSourceIds, knownStatIds, errors);
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
        ISet<string>? knownStatIds,
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
            ValidateStats(modifier.Stats, $"{path}.stats", index, knownStatIds, errors);
            ValidateSpawnWeights(modifier.SpawnWeights, $"{path}.spawnWeights", index, errors);
        }
    }

    private static void ValidateStats(
        IReadOnlyList<ModifierStat>? stats,
        string path,
        int modifierIndex,
        ISet<string>? knownStatIds,
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
            else if (knownStatIds is { Count: > 0 } && !knownStatIds.Contains(stat.StatId.Trim()))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ModifierStatIdUnknown,
                    $"{statPath}.statId",
                    $"Modifiers[{modifierIndex}].Stats[{statIndex}].StatId '{stat.StatId}' is not declared in package stats."));
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

    private static HashSet<string> ValidateStatDefinitions(
        IReadOnlyList<StatDefinition> stats,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nonNullStats = new List<(int Index, StatDefinition Stat, string Id)>();

        for (var index = 0; index < stats.Count; index++)
        {
            var path = $"stats[{index}]";
            StatDefinition? stat = stats[index];
            if (stat is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatRequired,
                    path,
                    $"Stats[{index}] is required."));
                continue;
            }

            var id = stat.Id?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatIdRequired,
                    $"{path}.id",
                    $"Stats[{index}].Id is required."));
            }
            else
            {
                if (!ids.Add(id))
                {
                    errors.Add(Error(
                        GameDataValidationErrorCodes.StatIdDuplicate,
                        $"{path}.id",
                        $"Stat Id '{id}' is duplicated."));
                }

                nonNullStats.Add((index, stat, id));
            }

            ValidateAlias(
                stat.MainHandAliasId,
                $"{path}.mainHandAliasId",
                id,
                GameDataValidationErrorCodes.StatMainHandAliasIdRequired,
                errors);
            ValidateAlias(
                stat.OffHandAliasId,
                $"{path}.offHandAliasId",
                id,
                GameDataValidationErrorCodes.StatOffHandAliasIdRequired,
                errors);

            ValidateSourceReferences(stat.Sources, $"{path}.sources", manifestSourceIds, errors);
        }

        foreach (var (index, stat, _) in nonNullStats)
        {
            ValidateAliasTarget(stat.MainHandAliasId, $"stats[{index}].mainHandAliasId", ids, errors);
            ValidateAliasTarget(stat.OffHandAliasId, $"stats[{index}].offHandAliasId", ids, errors);
        }

        return ids;
    }

    private static void ValidateAlias(
        string? aliasId,
        string path,
        string? ownerId,
        string requiredErrorCode,
        List<GameDataValidationError> errors)
    {
        if (aliasId is null)
        {
            return;
        }

        var normalizedAliasId = aliasId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAliasId))
        {
            errors.Add(Error(
                requiredErrorCode,
                path,
                $"Stat alias at {path} is required when present."));
            return;
        }

        if (!string.IsNullOrWhiteSpace(ownerId) &&
            string.Equals(normalizedAliasId, ownerId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.StatAliasIdSelfReference,
                path,
                $"Stat alias at {path} cannot reference its own stat Id."));
        }
    }

    private static void ValidateAliasTarget(
        string? aliasId,
        string path,
        ISet<string> knownStatIds,
        List<GameDataValidationError> errors)
    {
        var normalizedAliasId = aliasId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAliasId))
        {
            return;
        }

        if (!knownStatIds.Contains(normalizedAliasId))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.StatAliasIdUnknown,
                path,
                $"Stat alias at {path} references unknown stat Id '{normalizedAliasId}'."));
        }
    }

    private static void ValidateStatTranslations(
        IReadOnlyList<StatTranslationDefinition> translations,
        ISet<string> manifestSourceIds,
        ISet<string>? knownStatIds,
        List<GameDataValidationError> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < translations.Count; index++)
        {
            var path = $"statTranslations[{index}]";
            StatTranslationDefinition? translation = translations[index];
            if (translation is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationRequired,
                    path,
                    $"StatTranslations[{index}] is required."));
                continue;
            }

            var id = translation.Id?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationIdRequired,
                    $"{path}.id",
                    $"StatTranslations[{index}].Id is required."));
            }
            else if (!ids.Add(id))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationIdDuplicate,
                    $"{path}.id",
                    $"Stat translation Id '{id}' is duplicated."));
            }

            ValidateTranslationStatIds(translation.StatIds, path, index, knownStatIds, errors);
            ValidateTranslationVariants(translation.Variants, path, index, translation.StatIds?.Count ?? 0, errors);
            ValidateSourceReferences(translation.Sources, $"{path}.sources", manifestSourceIds, errors);
        }
    }

    private static void ValidateTranslationStatIds(
        IReadOnlyList<string>? statIds,
        string path,
        int translationIndex,
        ISet<string>? knownStatIds,
        List<GameDataValidationError> errors)
    {
        if (statIds is null || statIds.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.StatTranslationStatIdsRequired,
                $"{path}.statIds",
                $"StatTranslations[{translationIndex}].StatIds must contain at least one stat id."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < statIds.Count; index++)
        {
            var statId = statIds[index]?.Trim();
            if (string.IsNullOrWhiteSpace(statId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationStatIdRequired,
                    $"{path}.statIds[{index}]",
                    $"StatTranslations[{translationIndex}].StatIds[{index}] is required."));
                continue;
            }

            if (!seen.Add(statId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationStatIdDuplicate,
                    $"{path}.statIds[{index}]",
                    $"StatTranslations[{translationIndex}].StatIds[{index}] '{statId}' is duplicated."));
            }

            if (knownStatIds is { Count: > 0 } && !knownStatIds.Contains(statId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationStatIdUnknown,
                    $"{path}.statIds[{index}]",
                    $"StatTranslations[{translationIndex}].StatIds[{index}] '{statId}' is not declared in package stats."));
            }
        }
    }

    private static void ValidateTranslationVariants(
        IReadOnlyList<StatTranslationVariant>? variants,
        string path,
        int translationIndex,
        int statIdCount,
        List<GameDataValidationError> errors)
    {
        if (variants is null || variants.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.StatTranslationVariantsRequired,
                $"{path}.variants",
                $"StatTranslations[{translationIndex}].Variants must contain at least one variant."));
            return;
        }

        for (var variantIndex = 0; variantIndex < variants.Count; variantIndex++)
        {
            var variantPath = $"{path}.variants[{variantIndex}]";
            StatTranslationVariant? variant = variants[variantIndex];
            if (variant is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationVariantRequired,
                    variantPath,
                    $"StatTranslations[{translationIndex}].Variants[{variantIndex}] is required."));
                continue;
            }

            ValidateFormatLines(variant.FormatLines, variantPath, translationIndex, variantIndex, errors);
            ValidateValueFormats(variant.ValueFormats, variantPath, errors);
            ValidateConditions(variant.Conditions, variantPath, statIdCount, errors);
            ValidateIndexHandlers(variant.IndexHandlers, variantPath, statIdCount, errors);
        }
    }

    private static void ValidateFormatLines(
        IReadOnlyList<string>? formatLines,
        string variantPath,
        int translationIndex,
        int variantIndex,
        List<GameDataValidationError> errors)
    {
        if (formatLines is null || formatLines.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.StatTranslationFormatLinesRequired,
                $"{variantPath}.formatLines",
                $"StatTranslations[{translationIndex}].Variants[{variantIndex}].FormatLines must contain at least one line."));
            return;
        }

        for (var index = 0; index < formatLines.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(formatLines[index]))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationFormatLineRequired,
                    $"{variantPath}.formatLines[{index}]",
                    $"StatTranslations[{translationIndex}].Variants[{variantIndex}].FormatLines[{index}] is required."));
            }
        }
    }

    private static void ValidateValueFormats(
        IReadOnlyList<string>? valueFormats,
        string variantPath,
        List<GameDataValidationError> errors)
    {
        if (valueFormats is null)
        {
            return;
        }

        for (var index = 0; index < valueFormats.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(valueFormats[index]))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationValueFormatRequired,
                    $"{variantPath}.valueFormats[{index}]",
                    $"Stat translation value format at {variantPath}.ValueFormats[{index}] is required."));
            }
        }
    }

    private static void ValidateConditions(
        IReadOnlyList<StatTranslationCondition>? conditions,
        string variantPath,
        int statIdCount,
        List<GameDataValidationError> errors)
    {
        if (conditions is null)
        {
            return;
        }

        for (var index = 0; index < conditions.Count; index++)
        {
            var conditionPath = $"{variantPath}.conditions[{index}]";
            StatTranslationCondition? condition = conditions[index];
            if (condition is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationConditionRequired,
                    conditionPath,
                    $"Stat translation condition at {conditionPath} is required."));
                continue;
            }

            if (condition.Index < 0 || condition.Index >= statIdCount)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationConditionIndexInvalid,
                    $"{conditionPath}.index",
                    $"Stat translation condition at {conditionPath}.Index must reference a stat id index."));
            }

            if (condition.MinValue.HasValue &&
                condition.MaxValue.HasValue &&
                condition.MinValue.Value > condition.MaxValue.Value)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationConditionRangeInvalid,
                    conditionPath,
                    $"Stat translation condition at {conditionPath} has MinValue greater than MaxValue."));
            }
        }
    }

    private static void ValidateIndexHandlers(
        IReadOnlyList<StatTranslationIndexHandler>? indexHandlers,
        string variantPath,
        int statIdCount,
        List<GameDataValidationError> errors)
    {
        if (indexHandlers is null)
        {
            return;
        }

        for (var index = 0; index < indexHandlers.Count; index++)
        {
            var handlerPath = $"{variantPath}.indexHandlers[{index}]";
            StatTranslationIndexHandler? indexHandler = indexHandlers[index];
            if (indexHandler is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationIndexHandlerRequired,
                    handlerPath,
                    $"Stat translation index handler at {handlerPath} is required."));
                continue;
            }

            if (indexHandler.Index < 0 || indexHandler.Index >= statIdCount)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.StatTranslationIndexHandlerIndexInvalid,
                    $"{handlerPath}.index",
                    $"Stat translation index handler at {handlerPath}.Index must reference a stat id index."));
            }

            if (indexHandler.Handlers is null)
            {
                continue;
            }

            for (var handlerValueIndex = 0; handlerValueIndex < indexHandler.Handlers.Count; handlerValueIndex++)
            {
                if (string.IsNullOrWhiteSpace(indexHandler.Handlers[handlerValueIndex]))
                {
                    errors.Add(Error(
                        GameDataValidationErrorCodes.StatTranslationIndexHandlerValueRequired,
                        $"{handlerPath}.handlers[{handlerValueIndex}]",
                        $"Stat translation index handler at {handlerPath}.Handlers[{handlerValueIndex}] is required."));
                }
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

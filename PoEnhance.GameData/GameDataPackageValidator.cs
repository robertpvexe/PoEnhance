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

        HashSet<string>? knownModifierIds = null;
        if (package.Modifiers is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.PackageModifiersRequired,
                "modifiers",
                "Modifiers collection is required."));
        }
        else
        {
            knownModifierIds = ValidateModifiers(package.Modifiers, manifestSourceIds, knownStatIds, errors);
        }

        if (package.ItemBases is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.PackageItemBasesRequired,
                "itemBases",
                "ItemBases collection is required."));
        }
        else
        {
            ValidateItemBases(package.ItemBases, manifestSourceIds, knownModifierIds, errors);
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

        errors.AddRange(ValidateItemPropertySemantics(package.ItemPropertySemantics, package.Stats).Errors);

        return new GameDataValidationResult(errors);
    }

    public static GameDataValidationResult ValidateItemPropertySemantics(
        IReadOnlyList<ItemPropertySemanticDescriptor>? descriptors,
        IReadOnlyCollection<StatDefinition>? knownStats = null)
    {
        var errors = new List<GameDataValidationError>();
        if (descriptors is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.PackageItemPropertySemanticsRequired,
                "itemPropertySemantics",
                "ItemPropertySemantics collection is required."));
            return new GameDataValidationResult(errors);
        }

        ValidateItemPropertySemanticDescriptors(
            descriptors,
            BuildStatDefinitionIndex(knownStats),
            errors);
        return new GameDataValidationResult(errors);
    }

    private static void ValidateItemBases(
        IReadOnlyList<ItemBaseRecord> itemBases,
        ISet<string> manifestSourceIds,
        ISet<string>? knownModifierIds,
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

            ValidateImplicitModifierIds(itemBase.ImplicitModifierIds, $"{path}.implicitModifierIds", knownModifierIds, errors);

            ValidateItemBaseWeaponProperties(
                itemBase.WeaponProperties,
                $"{path}.weaponProperties",
                manifestSourceIds,
                errors);

            ValidateItemBaseDefenceProperties(
                itemBase.DefenceProperties,
                $"{path}.defenceProperties",
                manifestSourceIds,
                errors);

            ValidateSourceReferences(itemBase.Sources, $"{path}.sources", manifestSourceIds, errors);
        }
    }

    private static void ValidateItemBaseWeaponProperties(
        ItemBaseWeaponProperties? properties,
        string path,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        if (properties is null)
        {
            return;
        }

        if (properties.PhysicalDamageMinimum is < 0 ||
            properties.PhysicalDamageMaximum is < 0 ||
            properties.PhysicalDamageMinimum.HasValue != properties.PhysicalDamageMaximum.HasValue ||
            properties.PhysicalDamageMinimum > properties.PhysicalDamageMaximum)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemBaseWeaponPhysicalDamageInvalid,
                $"{path}.physicalDamageMinimum",
                "Weapon base Physical Damage requires a non-negative, ordered minimum and maximum pair."));
        }

        if (properties.AttackTimeMilliseconds is <= 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemBaseWeaponAttackTimeInvalid,
                $"{path}.attackTimeMilliseconds",
                "Weapon base attack time must be greater than zero when provided."));
        }

        if (properties.CriticalStrikeChancePercent is < 0m)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemBaseWeaponCriticalStrikeChanceInvalid,
                $"{path}.criticalStrikeChancePercent",
                "Weapon base Critical Strike Chance must be non-negative when provided."));
        }

        if (properties.Sources is null || properties.Sources.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemBaseWeaponSourcesRequired,
                $"{path}.sources",
                "Imported numerical weapon properties require source provenance."));
            return;
        }

        ValidateSourceReferences(properties.Sources, $"{path}.sources", manifestSourceIds, errors);
    }

    private static void ValidateItemBaseDefenceProperties(
        ItemBaseDefenceProperties? properties,
        string path,
        ISet<string> manifestSourceIds,
        List<GameDataValidationError> errors)
    {
        if (properties is null)
        {
            return;
        }

        ValidateDefenceRange(properties.EnergyShieldMinimum, properties.EnergyShieldMaximum, "energyShield", path, errors);
        ValidateDefenceRange(properties.ArmourMinimum, properties.ArmourMaximum, "armour", path, errors);
        ValidateDefenceRange(properties.EvasionRatingMinimum, properties.EvasionRatingMaximum, "evasionRating", path, errors);
        ValidateDefenceRange(properties.WardMinimum, properties.WardMaximum, "ward", path, errors);
        if (properties.ChanceToBlockPercent is < 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemBaseDefenceBlockInvalid,
                $"{path}.chanceToBlockPercent",
                "Base Chance to Block must be non-negative when provided."));
        }

        if (properties.Sources is null || properties.Sources.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemBaseDefenceSourcesRequired,
                $"{path}.sources",
                "Imported numerical defence properties require source provenance."));
            return;
        }

        ValidateSourceReferences(properties.Sources, $"{path}.sources", manifestSourceIds, errors);
    }

    private static void ValidateDefenceRange(
        int? minimum,
        int? maximum,
        string propertyName,
        string path,
        List<GameDataValidationError> errors)
    {
        if (minimum is < 0 || maximum is < 0 || minimum.HasValue != maximum.HasValue || minimum > maximum)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemBaseDefenceRangeInvalid,
                $"{path}.{propertyName}Minimum",
                $"Base {propertyName} requires a non-negative, ordered minimum and maximum pair."));
        }
    }

    private static HashSet<string> ValidateModifiers(
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

        return ids;
    }

    private static void ValidateImplicitModifierIds(
        IReadOnlyList<string>? implicitModifierIds,
        string path,
        ISet<string>? knownModifierIds,
        List<GameDataValidationError> errors)
    {
        if (implicitModifierIds is null || implicitModifierIds.Count == 0)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < implicitModifierIds.Count; index++)
        {
            var implicitId = implicitModifierIds[index]?.Trim();
            var implicitPath = $"{path}[{index}]";
            if (string.IsNullOrWhiteSpace(implicitId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseImplicitModifierIdRequired,
                    implicitPath,
                    $"Item base implicit modifier id at {implicitPath} is required."));
                continue;
            }

            if (!seen.Add(implicitId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseImplicitModifierIdDuplicate,
                    implicitPath,
                    $"Item base implicit modifier id '{implicitId}' is duplicated."));
            }

            if (knownModifierIds is { Count: > 0 } && !knownModifierIds.Contains(implicitId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemBaseImplicitModifierIdUnknown,
                    implicitPath,
                    $"Item base implicit modifier id '{implicitId}' is not declared in package modifiers."));
            }
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

    private static void ValidateItemPropertySemanticDescriptors(
        IReadOnlyList<ItemPropertySemanticDescriptor> descriptors,
        IReadOnlyDictionary<string, StatDefinition>? knownStats,
        List<GameDataValidationError> errors)
    {
        var descriptorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var statVectorKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < descriptors.Count; index++)
        {
            var path = $"itemPropertySemantics[{index}]";
            ItemPropertySemanticDescriptor? descriptor = descriptors[index];
            if (descriptor is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticRequired,
                    path,
                    $"ItemPropertySemantics[{index}] is required."));
                continue;
            }

            var descriptorId = descriptor.Id?.Trim();
            if (string.IsNullOrWhiteSpace(descriptorId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticIdRequired,
                    $"{path}.id",
                    $"ItemPropertySemantics[{index}].Id is required."));
            }
            else if (!descriptorIds.Add(descriptorId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticIdDuplicate,
                    $"{path}.id",
                    $"Item-property semantic descriptor Id '{descriptorId}' is duplicated."));
            }

            var normalizedStatIds = ValidateItemPropertySemanticStatIds(
                descriptor.OrderedStatIds,
                path,
                knownStats,
                errors);
            if (normalizedStatIds.Count > 0 && normalizedStatIds.Count == descriptor.OrderedStatIds?.Count)
            {
                var vectorKey = string.Join('\u001F', normalizedStatIds);
                if (!statVectorKeys.Add(vectorKey))
                {
                    errors.Add(Error(
                        GameDataValidationErrorCodes.ItemPropertySemanticStatVectorDuplicate,
                        $"{path}.orderedStatIds",
                        $"ItemPropertySemantics[{index}] duplicates an existing exact ordered stat vector."));
                }
            }

            if (!Enum.IsDefined(descriptor.Applicability))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticApplicabilityInvalid,
                    $"{path}.applicability",
                    $"ItemPropertySemantics[{index}].Applicability is invalid."));
            }

            ValidateItemPropertyContributions(descriptor.Contributions, path, errors);
            ValidateItemPropertySemanticEvidence(descriptor.Evidence, path, errors);

            if (descriptor.Applicability == ItemPropertyApplicability.UnconditionalDisplayedLocal &&
                knownStats is not null)
            {
                for (var statIndex = 0; statIndex < normalizedStatIds.Count; statIndex++)
                {
                    var statId = normalizedStatIds[statIndex];
                    if (knownStats.TryGetValue(statId, out var stat) && !stat.IsLocal)
                    {
                        errors.Add(Error(
                            GameDataValidationErrorCodes.ItemPropertySemanticUnconditionalStatNotLocal,
                            $"{path}.orderedStatIds[{statIndex}]",
                            $"Unconditional displayed-local semantic references non-local stat Id '{statId}'."));
                    }
                }
            }
        }
    }

    private static IReadOnlyList<string> ValidateItemPropertySemanticStatIds(
        IReadOnlyList<string>? statIds,
        string descriptorPath,
        IReadOnlyDictionary<string, StatDefinition>? knownStats,
        List<GameDataValidationError> errors)
    {
        if (statIds is null || statIds.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemPropertySemanticStatIdsRequired,
                $"{descriptorPath}.orderedStatIds",
                $"{descriptorPath}.OrderedStatIds must contain at least one stat Id."));
            return [];
        }

        var normalizedStatIds = new List<string>(statIds.Count);
        var seenStatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < statIds.Count; index++)
        {
            var path = $"{descriptorPath}.orderedStatIds[{index}]";
            var statId = statIds[index]?.Trim();
            if (string.IsNullOrWhiteSpace(statId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticStatIdRequired,
                    path,
                    $"Item-property semantic stat Id at {path} is required."));
                continue;
            }

            normalizedStatIds.Add(statId);
            if (!seenStatIds.Add(statId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticStatIdDuplicate,
                    path,
                    $"Item-property semantic stat Id '{statId}' is duplicated in its ordered vector."));
            }

            if (knownStats is not null && !knownStats.ContainsKey(statId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticStatIdUnknown,
                    path,
                    $"Item-property semantic stat Id '{statId}' is not declared in package stats."));
            }
        }

        return normalizedStatIds;
    }

    private static void ValidateItemPropertyContributions(
        IReadOnlyList<ItemPropertyContribution>? contributions,
        string descriptorPath,
        List<GameDataValidationError> errors)
    {
        var path = $"{descriptorPath}.contributions";
        if (contributions is null || contributions.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemPropertySemanticContributionsRequired,
                path,
                $"{descriptorPath}.Contributions must contain at least one contribution."));
            return;
        }

        var contributionKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < contributions.Count; index++)
        {
            var contributionPath = $"{path}[{index}]";
            ItemPropertyContribution? contribution = contributions[index];
            if (contribution is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticContributionRequired,
                    contributionPath,
                    $"Item-property semantic contribution at {contributionPath} is required."));
                continue;
            }

            if (!Enum.IsDefined(contribution.Operation) || contribution.Operation == ItemPropertyOperation.Unknown)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticContributionOperationInvalid,
                    $"{contributionPath}.operation",
                    $"Item-property semantic contribution operation at {contributionPath} is invalid."));
            }

            var normalizedTargets = ValidateItemPropertyTargets(
                contribution.Targets,
                contributionPath,
                errors);
            if (normalizedTargets.Count == 0)
            {
                continue;
            }

            var contributionKey = $"{(int)contribution.Operation}\u001F{string.Join(',', normalizedTargets.Order())}";
            if (!contributionKeys.Add(contributionKey))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticContributionDuplicate,
                    contributionPath,
                    $"Item-property semantic contribution at {contributionPath} is duplicated."));
            }
        }
    }

    private static IReadOnlyList<int> ValidateItemPropertyTargets(
        IReadOnlyList<ItemPropertyTarget>? targets,
        string contributionPath,
        List<GameDataValidationError> errors)
    {
        var path = $"{contributionPath}.targets";
        if (targets is null || targets.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemPropertySemanticContributionTargetsRequired,
                path,
                $"Item-property semantic contribution targets at {path} must contain at least one target."));
            return [];
        }

        var normalizedTargets = new List<int>(targets.Count);
        var seenTargets = new HashSet<ItemPropertyTarget>();
        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            var targetPath = $"{path}[{index}]";
            if (!Enum.IsDefined(target) || target == ItemPropertyTarget.Unknown)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticContributionTargetInvalid,
                    targetPath,
                    $"Item-property semantic contribution target at {targetPath} is invalid."));
                continue;
            }

            normalizedTargets.Add((int)target);
            if (!seenTargets.Add(target))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticContributionTargetDuplicate,
                    targetPath,
                    $"Item-property semantic contribution target '{target}' is duplicated."));
            }
        }

        return normalizedTargets;
    }

    private static void ValidateItemPropertySemanticEvidence(
        IReadOnlyList<ItemPropertySemanticEvidence>? evidence,
        string descriptorPath,
        List<GameDataValidationError> errors)
    {
        var path = $"{descriptorPath}.evidence";
        if (evidence is null || evidence.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ItemPropertySemanticEvidenceRequired,
                path,
                $"{descriptorPath}.Evidence must contain at least one entry."));
            return;
        }

        for (var index = 0; index < evidence.Count; index++)
        {
            var evidencePath = $"{path}[{index}]";
            ItemPropertySemanticEvidence? entry = evidence[index];
            if (entry is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticEvidenceEntryRequired,
                    evidencePath,
                    $"Item-property semantic evidence at {evidencePath} is required."));
                continue;
            }

            if (!Enum.IsDefined(entry.Method) || entry.Method == ItemPropertySemanticEvidenceMethod.Unknown)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticEvidenceMethodInvalid,
                    $"{evidencePath}.method",
                    $"Item-property semantic evidence method at {evidencePath} is invalid."));
            }

            if (string.IsNullOrWhiteSpace(entry.SourceId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticEvidenceSourceIdRequired,
                    $"{evidencePath}.sourceId",
                    $"Item-property semantic evidence SourceId at {evidencePath} is required."));
            }

            if (string.IsNullOrWhiteSpace(entry.ReviewVersion))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticEvidenceReviewVersionRequired,
                    $"{evidencePath}.reviewVersion",
                    $"Item-property semantic evidence ReviewVersion at {evidencePath} is required."));
            }

            if (string.IsNullOrWhiteSpace(entry.ReviewReference))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ItemPropertySemanticEvidenceReviewReferenceRequired,
                    $"{evidencePath}.reviewReference",
                    $"Item-property semantic evidence ReviewReference at {evidencePath} is required."));
            }
        }
    }

    private static IReadOnlyDictionary<string, StatDefinition>? BuildStatDefinitionIndex(
        IReadOnlyCollection<StatDefinition>? stats)
    {
        if (stats is null)
        {
            return null;
        }

        var index = new Dictionary<string, StatDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var stat in stats)
        {
            if (stat is null)
            {
                continue;
            }

            var statId = stat.Id?.Trim();
            if (!string.IsNullOrWhiteSpace(statId))
            {
                index.TryAdd(statId, stat);
            }
        }

        return index;
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

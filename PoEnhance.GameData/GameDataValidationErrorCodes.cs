namespace PoEnhance.GameData;

public static class GameDataValidationErrorCodes
{
    public const string PackageRequired = "package.required";
    public const string PackageItemBasesRequired = "package.itemBases.required";
    public const string PackageModifiersRequired = "package.modifiers.required";
    public const string PackageStatsRequired = "package.stats.required";
    public const string PackageStatTranslationsRequired = "package.statTranslations.required";
    public const string PackageItemPropertySemanticsRequired = "package.itemPropertySemantics.required";

    public const string ManifestRequired = "manifest.required";
    public const string ManifestSchemaVersionInvalid = "manifest.schemaVersion.invalid";
    public const string ManifestDataVersionRequired = "manifest.dataVersion.required";
    public const string ManifestCreatedAtUtcNotUtc = "manifest.createdAtUtc.notUtc";
    public const string ManifestSourcesRequired = "manifest.sources.required";
    public const string ManifestSourceRequired = "manifest.source.required";
    public const string ManifestSourceIdRequired = "manifest.source.sourceId.required";
    public const string ManifestSourceIdDuplicate = "manifest.source.sourceId.duplicate";
    public const string ManifestSourceRetrievedAtUtcNotUtc = "manifest.source.retrievedAtUtc.notUtc";
    public const string ManifestReviewedItemPropertySemanticsSourceIdRequired = "manifest.reviewedItemPropertySemantics.sourceId.required";
    public const string ManifestReviewedItemPropertySemanticsLabelRequired = "manifest.reviewedItemPropertySemantics.label.required";
    public const string ManifestReviewedItemPropertySemanticsDisplayPathRequired = "manifest.reviewedItemPropertySemantics.displayPath.required";
    public const string ManifestReviewedItemPropertySemanticsSizeInvalid = "manifest.reviewedItemPropertySemantics.sizeBytes.invalid";
    public const string ManifestReviewedItemPropertySemanticsSha256Invalid = "manifest.reviewedItemPropertySemantics.sha256.invalid";
    public const string ManifestReviewedItemPropertySemanticsSchemaVersionInvalid = "manifest.reviewedItemPropertySemantics.schemaVersion.invalid";
    public const string ManifestReviewedItemPropertySemanticsReviewVersionRequired = "manifest.reviewedItemPropertySemantics.reviewVersion.required";

    public const string ItemBaseRequired = "itemBase.required";
    public const string ItemBaseIdRequired = "itemBase.id.required";
    public const string ItemBaseIdDuplicate = "itemBase.id.duplicate";
    public const string ItemBaseNameRequired = "itemBase.name.required";
    public const string ItemBaseItemClassRequired = "itemBase.itemClass.required";
    public const string ItemBaseRequiredLevelNegative = "itemBase.requiredLevel.negative";
    public const string ItemBaseTagRequired = "itemBase.tag.required";
    public const string ItemBaseTagDuplicate = "itemBase.tag.duplicate";
    public const string ItemBaseImplicitModifierIdRequired = "itemBase.implicitModifierId.required";
    public const string ItemBaseImplicitModifierIdDuplicate = "itemBase.implicitModifierId.duplicate";
    public const string ItemBaseImplicitModifierIdUnknown = "itemBase.implicitModifierId.unknown";

    public const string ModifierRequired = "modifier.required";
    public const string ModifierIdRequired = "modifier.id.required";
    public const string ModifierIdDuplicate = "modifier.id.duplicate";
    public const string ModifierGroupIdRequired = "modifier.groupId.required";
    public const string ModifierTierInvalid = "modifier.tier.invalid";
    public const string ModifierRequiredLevelNegative = "modifier.requiredLevel.negative";
    public const string ModifierTagRequired = "modifier.tag.required";
    public const string ModifierTagDuplicate = "modifier.tag.duplicate";
    public const string ModifierStatsRequired = "modifier.stats.required";
    public const string ModifierStatRequired = "modifier.stat.required";
    public const string ModifierStatIdRequired = "modifier.stat.statId.required";
    public const string ModifierStatIndexNegative = "modifier.stat.index.negative";
    public const string ModifierStatIndexDuplicate = "modifier.stat.index.duplicate";
    public const string ModifierStatRangeInvalid = "modifier.stat.range.invalid";
    public const string ModifierSpawnWeightRequired = "modifier.spawnWeight.required";
    public const string ModifierSpawnWeightTagRequired = "modifier.spawnWeight.tag.required";
    public const string ModifierSpawnWeightWeightNegative = "modifier.spawnWeight.weight.negative";
    public const string ModifierSpawnWeightTagDuplicate = "modifier.spawnWeight.tag.duplicate";
    public const string ModifierStatIdUnknown = "modifier.stat.statId.unknown";

    public const string StatRequired = "stat.required";
    public const string StatIdRequired = "stat.id.required";
    public const string StatIdDuplicate = "stat.id.duplicate";
    public const string StatMainHandAliasIdRequired = "stat.mainHandAliasId.required";
    public const string StatOffHandAliasIdRequired = "stat.offHandAliasId.required";
    public const string StatAliasIdSelfReference = "stat.aliasId.selfReference";
    public const string StatAliasIdUnknown = "stat.aliasId.unknown";

    public const string StatTranslationRequired = "statTranslation.required";
    public const string StatTranslationIdRequired = "statTranslation.id.required";
    public const string StatTranslationIdDuplicate = "statTranslation.id.duplicate";
    public const string StatTranslationStatIdsRequired = "statTranslation.statIds.required";
    public const string StatTranslationStatIdRequired = "statTranslation.statId.required";
    public const string StatTranslationStatIdDuplicate = "statTranslation.statId.duplicate";
    public const string StatTranslationStatIdUnknown = "statTranslation.statId.unknown";
    public const string StatTranslationVariantsRequired = "statTranslation.variants.required";
    public const string StatTranslationVariantRequired = "statTranslation.variant.required";
    public const string StatTranslationFormatLinesRequired = "statTranslation.formatLines.required";
    public const string StatTranslationFormatLineRequired = "statTranslation.formatLine.required";
    public const string StatTranslationConditionRequired = "statTranslation.condition.required";
    public const string StatTranslationConditionIndexInvalid = "statTranslation.condition.index.invalid";
    public const string StatTranslationConditionRangeInvalid = "statTranslation.condition.range.invalid";
    public const string StatTranslationIndexHandlerRequired = "statTranslation.indexHandler.required";
    public const string StatTranslationIndexHandlerIndexInvalid = "statTranslation.indexHandler.index.invalid";
    public const string StatTranslationIndexHandlerValueRequired = "statTranslation.indexHandler.value.required";
    public const string StatTranslationValueFormatRequired = "statTranslation.valueFormat.required";

    public const string ItemPropertySemanticRequired = "itemPropertySemantic.required";
    public const string ItemPropertySemanticIdRequired = "itemPropertySemantic.id.required";
    public const string ItemPropertySemanticIdDuplicate = "itemPropertySemantic.id.duplicate";
    public const string ItemPropertySemanticStatIdsRequired = "itemPropertySemantic.statIds.required";
    public const string ItemPropertySemanticStatIdRequired = "itemPropertySemantic.statId.required";
    public const string ItemPropertySemanticStatIdDuplicate = "itemPropertySemantic.statId.duplicate";
    public const string ItemPropertySemanticStatIdUnknown = "itemPropertySemantic.statId.unknown";
    public const string ItemPropertySemanticStatVectorDuplicate = "itemPropertySemantic.statVector.duplicate";
    public const string ItemPropertySemanticContributionsRequired = "itemPropertySemantic.contributions.required";
    public const string ItemPropertySemanticContributionRequired = "itemPropertySemantic.contribution.required";
    public const string ItemPropertySemanticContributionDuplicate = "itemPropertySemantic.contribution.duplicate";
    public const string ItemPropertySemanticContributionOperationInvalid = "itemPropertySemantic.contribution.operation.invalid";
    public const string ItemPropertySemanticContributionTargetsRequired = "itemPropertySemantic.contribution.targets.required";
    public const string ItemPropertySemanticContributionTargetInvalid = "itemPropertySemantic.contribution.target.invalid";
    public const string ItemPropertySemanticContributionTargetDuplicate = "itemPropertySemantic.contribution.target.duplicate";
    public const string ItemPropertySemanticApplicabilityInvalid = "itemPropertySemantic.applicability.invalid";
    public const string ItemPropertySemanticEvidenceRequired = "itemPropertySemantic.evidence.required";
    public const string ItemPropertySemanticEvidenceEntryRequired = "itemPropertySemantic.evidence.entry.required";
    public const string ItemPropertySemanticEvidenceMethodInvalid = "itemPropertySemantic.evidence.method.invalid";
    public const string ItemPropertySemanticEvidenceSourceIdRequired = "itemPropertySemantic.evidence.sourceId.required";
    public const string ItemPropertySemanticEvidenceReviewVersionRequired = "itemPropertySemantic.evidence.reviewVersion.required";
    public const string ItemPropertySemanticEvidenceReviewReferenceRequired = "itemPropertySemantic.evidence.reviewReference.required";
    public const string ItemPropertySemanticUnconditionalStatNotLocal = "itemPropertySemantic.unconditional.stat.notLocal";

    public const string SourceReferenceRequired = "sourceReference.required";
    public const string SourceReferenceSourceIdRequired = "sourceReference.sourceId.required";
    public const string SourceReferenceSourceIdUnknown = "sourceReference.sourceId.unknown";
    public const string SourceReferenceDuplicate = "sourceReference.duplicate";
}

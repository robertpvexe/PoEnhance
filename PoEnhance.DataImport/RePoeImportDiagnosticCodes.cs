namespace PoEnhance.DataImport;

public static class RePoeImportDiagnosticCodes
{
    public const string FileNotFound = "REPOE_FILE_NOT_FOUND";
    public const string JsonMalformed = "REPOE_JSON_MALFORMED";
    public const string SchemaUnsupported = "REPOE_SCHEMA_UNSUPPORTED";
    public const string RecordUnsupported = "REPOE_RECORD_UNSUPPORTED";
    public const string RecordMissingId = "REPOE_RECORD_MISSING_ID";
    public const string RecordMissingName = "REPOE_RECORD_MISSING_NAME";
    public const string RecordMissingItemClass = "REPOE_RECORD_MISSING_ITEM_CLASS";
    public const string RecordInvalidRequiredLevel = "REPOE_RECORD_INVALID_REQUIRED_LEVEL";
    public const string RecordInvalidTags = "REPOE_RECORD_INVALID_TAGS";
    public const string RecordInvalidImplicits = "REPOE_RECORD_INVALID_IMPLICITS";
    public const string ModifierRecordMissingId = "REPOE_MODIFIER_RECORD_MISSING_ID";
    public const string ModifierRecordUnsupported = "REPOE_MODIFIER_RECORD_UNSUPPORTED";
    public const string ModifierRecordMissingGroup = "REPOE_MODIFIER_RECORD_MISSING_GROUP";
    public const string ModifierRecordMissingStats = "REPOE_MODIFIER_RECORD_MISSING_STATS";
    public const string ModifierRecordInvalidStat = "REPOE_MODIFIER_RECORD_INVALID_STAT";
    public const string ModifierRecordInvalidSpawnWeight = "REPOE_MODIFIER_RECORD_INVALID_SPAWN_WEIGHT";
    public const string ModifierRecordInvalidTags = "REPOE_MODIFIER_RECORD_INVALID_TAGS";
    public const string StatRecordMissingId = "REPOE_STAT_RECORD_MISSING_ID";
    public const string StatRecordUnsupported = "REPOE_STAT_RECORD_UNSUPPORTED";
    public const string StatRecordMissingIsLocal = "REPOE_STAT_RECORD_MISSING_IS_LOCAL";
    public const string StatRecordInvalidAlias = "REPOE_STAT_RECORD_INVALID_ALIAS";
    public const string StatTranslationRecordUnsupported = "REPOE_STAT_TRANSLATION_RECORD_UNSUPPORTED";
    public const string StatTranslationMissingStatIds = "REPOE_STAT_TRANSLATION_MISSING_STAT_IDS";
    public const string StatTranslationDuplicateStatId = "REPOE_STAT_TRANSLATION_DUPLICATE_STAT_ID";
    public const string StatTranslationUnknownStatId = "REPOE_STAT_TRANSLATION_UNKNOWN_STAT_ID";
    public const string StatTranslationMissingVariants = "REPOE_STAT_TRANSLATION_MISSING_VARIANTS";
    public const string StatTranslationInvalidCondition = "REPOE_STAT_TRANSLATION_INVALID_CONDITION";
    public const string StatTranslationInvalidFormat = "REPOE_STAT_TRANSLATION_INVALID_FORMAT";
    public const string StatTranslationInvalidIndexHandler = "REPOE_STAT_TRANSLATION_INVALID_INDEX_HANDLER";

    public const string PackageManifestInvalid = "PACKAGE_MANIFEST_INVALID";
    public const string PackageRePoeSourceMissing = "PACKAGE_REPOE_SOURCE_MISSING";
    public const string PackageValidationFailed = "PACKAGE_VALIDATION_FAILED";
    public const string PackageModifierStatReferenceMissing = "PACKAGE_MODIFIER_STAT_REFERENCE_MISSING";
    public const string PackageStatAliasReferenceMissing = "PACKAGE_STAT_ALIAS_REFERENCE_MISSING";
    public const string PackageTranslationStatReferenceMissing = "PACKAGE_TRANSLATION_STAT_REFERENCE_MISSING";
    public const string PackageBaseImplicitModifierReferenceMissing = "PACKAGE_BASE_IMPLICIT_MODIFIER_REFERENCE_MISSING";
    public const string BuildArgumentInvalid = "BUILD_ARGUMENT_INVALID";
    public const string BuildInputFileMissing = "BUILD_INPUT_FILE_MISSING";
    public const string BuildOutputWriteFailed = "BUILD_OUTPUT_WRITE_FAILED";
}

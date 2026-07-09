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

    public const string PackageManifestInvalid = "PACKAGE_MANIFEST_INVALID";
    public const string PackageRePoeSourceMissing = "PACKAGE_REPOE_SOURCE_MISSING";
    public const string PackageValidationFailed = "PACKAGE_VALIDATION_FAILED";
}

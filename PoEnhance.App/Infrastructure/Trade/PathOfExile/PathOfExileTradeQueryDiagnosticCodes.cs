namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeQueryDiagnosticCodes
{
    public const string NullDraft = "POE_TRADE_QUERY_NULL_DRAFT";

    public const string NullValidationResult = "POE_TRADE_QUERY_NULL_VALIDATION_RESULT";

    public const string LocallyInvalidDraft = "POE_TRADE_QUERY_LOCALLY_INVALID_DRAFT";

    public const string MissingLeague = "POE_TRADE_QUERY_MISSING_LEAGUE";

    public const string MissingBaseIdentity = "POE_TRADE_QUERY_MISSING_BASE_IDENTITY";

    public const string SelectedModifiersMissingProviderMapping = "POE_TRADE_QUERY_SELECTED_MODIFIERS_MISSING_PROVIDER_MAPPING";

    public const string SelectedModifierMappingMismatch = "POE_TRADE_QUERY_SELECTED_MODIFIER_MAPPING_MISMATCH";

    public const string InvalidSelectedModifierMapping = "POE_TRADE_QUERY_INVALID_SELECTED_MODIFIER_MAPPING";

    public const string UnsupportedRarityOrItemPath = "POE_TRADE_QUERY_UNSUPPORTED_RARITY_OR_ITEM_PATH";

    public const string MissingUniqueName = "POE_TRADE_QUERY_MISSING_UNIQUE_NAME";
}

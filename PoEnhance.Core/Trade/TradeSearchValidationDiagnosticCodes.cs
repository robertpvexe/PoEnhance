namespace PoEnhance.Core.Trade;

public static class TradeSearchValidationDiagnosticCodes
{
    public const string NullDraft = "TRADE_VALIDATION_NULL_DRAFT";

    public const string MissingBaseIdentity = "TRADE_VALIDATION_MISSING_BASE_IDENTITY";

    public const string UnresolvedBase = "TRADE_VALIDATION_UNRESOLVED_BASE";

    public const string NegativeItemLevel = "TRADE_VALIDATION_NEGATIVE_ITEM_LEVEL";

    public const string SelectedModifierMissingText = "TRADE_VALIDATION_SELECTED_MODIFIER_MISSING_TEXT";

    public const string SelectedModifierUnresolved = "TRADE_VALIDATION_SELECTED_MODIFIER_UNRESOLVED";

    public const string InvalidModifierRange = "TRADE_VALIDATION_INVALID_MODIFIER_RANGE";

    public const string UnsupportedSpecialItemFact = "TRADE_VALIDATION_UNSUPPORTED_SPECIAL_ITEM_FACT";
}

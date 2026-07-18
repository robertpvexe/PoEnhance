namespace PoEnhance.Core.Trade;

public static class TradeSearchValidationDiagnosticCodes
{
    public const string NullDraft = "TRADE_VALIDATION_NULL_DRAFT";

    public const string MissingBaseIdentity = "TRADE_VALIDATION_MISSING_BASE_IDENTITY";

    public const string UnresolvedBase = "TRADE_VALIDATION_UNRESOLVED_BASE";

    public const string NegativeItemLevel = "TRADE_VALIDATION_NEGATIVE_ITEM_LEVEL";

    public const string RequestedItemFilterInvalid = "TRADE_VALIDATION_REQUESTED_ITEM_FILTER_INVALID";

    public const string RequestedItemFilterUnresolved = "TRADE_VALIDATION_REQUESTED_ITEM_FILTER_UNRESOLVED";

    public const string RequestedItemFilterUnsupported = "TRADE_VALIDATION_REQUESTED_ITEM_FILTER_UNSUPPORTED";

    public const string SelectedItemPropertyUnresolved =
        "TRADE_VALIDATION_SELECTED_ITEM_PROPERTY_UNRESOLVED";

    public const string SelectedItemPropertyUnsupported =
        "TRADE_VALIDATION_SELECTED_ITEM_PROPERTY_UNSUPPORTED";

    public const string SelectedItemPropertyAmbiguous =
        "TRADE_VALIDATION_SELECTED_ITEM_PROPERTY_AMBIGUOUS";

    public const string InvalidItemPropertyRange =
        "TRADE_VALIDATION_INVALID_ITEM_PROPERTY_RANGE";

    public const string SelectedModifierMissingText = "TRADE_VALIDATION_SELECTED_MODIFIER_MISSING_TEXT";

    public const string SelectedModifierUnresolved = "TRADE_VALIDATION_SELECTED_MODIFIER_UNRESOLVED";

    public const string SelectedModifierVariantUnresolved =
        "TRADE_VALIDATION_SELECTED_MODIFIER_VARIANT_UNRESOLVED";

    public const string SelectedModifierBoundsUnsupported =
        "TRADE_VALIDATION_SELECTED_MODIFIER_BOUNDS_UNSUPPORTED";

    public const string SelectedSpecialModifierUnsupported =
        "TRADE_VALIDATION_SELECTED_SPECIAL_MODIFIER_UNSUPPORTED";

    public const string SelectedModifierRepresentedByExactBase =
        "TRADE_VALIDATION_SELECTED_MODIFIER_REPRESENTED_BY_EXACT_BASE";

    public const string InvalidModifierRange = "TRADE_VALIDATION_INVALID_MODIFIER_RANGE";

    public const string InvalidContributorVariant = "TRADE_VALIDATION_INVALID_CONTRIBUTOR_VARIANT";

    public const string InvalidContributorSourceIdentity =
        "TRADE_VALIDATION_INVALID_CONTRIBUTOR_SOURCE_IDENTITY";

    public const string InvalidContributorMinimum =
        "TRADE_VALIDATION_INVALID_CONTRIBUTOR_MINIMUM";

    public const string InvalidContributorRange = "TRADE_VALIDATION_INVALID_CONTRIBUTOR_RANGE";

    public const string InvalidContributorParent = "TRADE_VALIDATION_INVALID_CONTRIBUTOR_PARENT";

    public const string InvalidContributorParentFloor = "TRADE_VALIDATION_INVALID_CONTRIBUTOR_PARENT_FLOOR";

    public const string UnsupportedContributorProjection =
        "TRADE_VALIDATION_UNSUPPORTED_CONTRIBUTOR_PROJECTION";

    public const string UnsupportedSpecialItemFact = "TRADE_VALIDATION_UNSUPPORTED_SPECIAL_ITEM_FACT";
}

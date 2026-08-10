namespace PoEnhance.Core.Items.GameData;

public static class ModifierTextSignatureMatchReasonCodes
{
    public const string Match = "MODIFIER_TEXT_SIGNATURE_MATCH";
    public const string NoMatch = "MODIFIER_TEXT_SIGNATURE_NO_MATCH";
    public const string ParsedSignatureEmpty = "MODIFIER_TEXT_PARSED_SIGNATURE_EMPTY";
    public const string ParsedSignatureUnsupported = "MODIFIER_TEXT_PARSED_SIGNATURE_UNSUPPORTED";
    public const string ModifierStatsMissing = "MODIFIER_TEXT_MODIFIER_STATS_MISSING";
    public const string TranslationMissing = "MODIFIER_TEXT_TRANSLATION_MISSING";
    public const string TranslationAmbiguous = "MODIFIER_TEXT_TRANSLATION_AMBIGUOUS";
    public const string TranslationShapeUnsupported = "MODIFIER_TEXT_TRANSLATION_SHAPE_UNSUPPORTED";
    public const string TranslationConditionUnsupported = "MODIFIER_TEXT_TRANSLATION_CONDITION_UNSUPPORTED";
    public const string TranslationConditionUnresolved = "MODIFIER_TEXT_TRANSLATION_CONDITION_UNRESOLVED";
    public const string TranslationRenderingUnsupported = "MODIFIER_TEXT_TRANSLATION_RENDERING_UNSUPPORTED";
    public const string HistoricalTranslationMatch = "MODIFIER_TEXT_HISTORICAL_TRANSLATION_MATCH";
    public const string HistoricalTranslationAmbiguous = "MODIFIER_TEXT_HISTORICAL_TRANSLATION_AMBIGUOUS";
    public const string HistoricalTranslationOriginIneligible = "MODIFIER_TEXT_HISTORICAL_TRANSLATION_ORIGIN_INELIGIBLE";
}

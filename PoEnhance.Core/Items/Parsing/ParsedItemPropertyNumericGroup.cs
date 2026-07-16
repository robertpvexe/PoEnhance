namespace PoEnhance.Core.Items.Parsing;

public sealed record ParsedItemPropertyNumericGroup(
    string OriginalText,
    decimal? ScalarValue,
    decimal? MinimumValue,
    decimal? MaximumValue,
    bool IsPercentage,
    bool IsAugmented)
{
    public bool IsScalar => ScalarValue.HasValue;

    public bool IsRange => MinimumValue.HasValue && MaximumValue.HasValue;
}

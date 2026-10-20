namespace PoEnhance.Core.Trade;

/// <summary>
/// Structured quality basis for Trade searchability. Display values may still use
/// <see cref="TradeSearchItemProperty.ObservedValue"/> when normalization is unavailable.
/// </summary>
public enum TradeSearchItemPropertyQualityNormalizationStatus
{
    /// <summary>Property is not quality-sensitive for Trade (e.g. APS, Crit, Elemental DPS).</summary>
    NotApplicable = 0,

    /// <summary>Value is proven at maximum quality (Q20) for Trade filters.</summary>
    NormalizedToTwenty = 1,

    /// <summary>
    /// Quality-sensitive property whose Q20 reconstruction is unavailable. Observed value may be
    /// displayed, but must not become a Trade filter default.
    /// </summary>
    Unavailable = 2,
}

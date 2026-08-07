namespace PoEnhance.GameData;

/// <summary>
/// Summarizes source-level spawn-weight evidence without replacing context-specific
/// item-base eligibility evaluation.
/// </summary>
public enum ModifierSourceAvailability
{
    /// <summary>
    /// No reliable conclusion can be made from usable source spawn-weight evidence.
    /// </summary>
    Unknown,

    /// <summary>
    /// At least one source spawn-weight entry has a positive weight.
    /// </summary>
    PotentiallyEligible,

    /// <summary>
    /// At least one usable source spawn-weight entry exists and every weight is zero.
    /// </summary>
    Disabled,
}

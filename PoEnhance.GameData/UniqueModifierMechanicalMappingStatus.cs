namespace PoEnhance.GameData;

public enum UniqueModifierMechanicalMappingStatus
{
    Unknown,
    Exact,
    EquivalentSourceSet,
    Ambiguous,
    Unsupported,
    /// <summary>
    /// Source-proven display/requirement information that must never become a Trade stat filter
    /// (for example Forbidden Flame/Flesh <c>Requires Class …</c>).
    /// </summary>
    NonSearchable,
}

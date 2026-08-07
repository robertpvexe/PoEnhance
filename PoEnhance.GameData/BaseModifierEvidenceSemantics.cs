namespace PoEnhance.GameData;

public enum BaseModifierEvidenceSemantics
{
    Unknown,

    /// <summary>
    /// Relationships are positive or context-dependent source evidence only. Absence
    /// of a relationship is not evidence that a modifier is ineligible for a base.
    /// </summary>
    PositiveAndContextualOnly,
}

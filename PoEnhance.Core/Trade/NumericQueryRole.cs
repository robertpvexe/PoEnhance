namespace PoEnhance.Core.Trade;

/// <summary>
/// Structured numeric query semantics derived from GameData translation/stat evidence.
/// </summary>
public enum NumericQueryRole
{
    Unknown = 0,
    OrdinaryScalar = 1,
    SkillGemLevelThreshold = 2,
    CoupledRatio = 3,
    PresenceOnly = 4,
}

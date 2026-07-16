namespace PoEnhance.GameData;

public enum ItemPropertyApplicability
{
    Unknown = 0,
    UnconditionalDisplayedLocal = 1,
    Conditional = 2,
    SpellOnly = 3,
    AttackOnNonWeapon = 4,
    Global = 5,
    Minion = 6,
    DamageOverTime = 7,
    Reflected = 8,
    Unsupported = 9,
}

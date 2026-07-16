using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedWeaponDamage(
    ParsedItemProperty SourceProperty,
    IReadOnlyList<DerivedWeaponDamageRange> Ranges,
    decimal AverageHit,
    decimal DamagePerSecond);

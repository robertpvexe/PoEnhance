using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedWeaponDamageRange(
    ParsedItemPropertyNumericGroup SourceGroup,
    decimal AverageHit);

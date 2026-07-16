using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedWeaponPropertyDiagnostic(
    string Code,
    string Reason,
    ParsedItemProperty? SourceProperty = null);

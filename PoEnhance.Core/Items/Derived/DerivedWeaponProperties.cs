using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedWeaponProperties
{
    public DerivedWeaponPropertyStatus Status { get; init; }

    public DerivedWeaponDamage? PhysicalDamage { get; init; }

    public DerivedWeaponDamage? ElementalDamage { get; init; }

    public DerivedWeaponDamage? ChaosDamage { get; init; }

    public decimal? PhysicalDps => PhysicalDamage?.DamagePerSecond;

    public decimal? ElementalDps => ElementalDamage?.DamagePerSecond;

    public decimal? ChaosDps => ChaosDamage?.DamagePerSecond;

    public decimal? TotalDps { get; init; }

    public decimal? AttacksPerSecond { get; init; }

    public decimal? CriticalStrikeChance { get; init; }

    public ParsedItemProperty? AttacksPerSecondSourceProperty { get; init; }

    public ParsedItemProperty? CriticalStrikeChanceSourceProperty { get; init; }

    public IReadOnlyList<DerivedWeaponPropertyDiagnostic> Diagnostics { get; init; } = [];

    public DerivedWeaponQ20Status Q20Status { get; init; }

    public DerivedWeaponQ20Provenance? Q20Provenance { get; init; }
}

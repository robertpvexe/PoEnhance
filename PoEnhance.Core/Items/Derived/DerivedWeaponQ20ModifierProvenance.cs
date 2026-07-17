using PoEnhance.GameData;

namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedWeaponQ20ModifierProvenance
{
    public required string ComponentId { get; init; }

    public int SourceModifierIndex { get; init; }

    public string? ResolvedModifierId { get; init; }

    public string? ReviewedSemanticDescriptorId { get; init; }

    public ItemPropertyOperation Operation { get; init; }

    public IReadOnlyList<decimal> CanonicalNumericValues { get; init; } = [];
}

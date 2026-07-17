using PoEnhance.GameData;

namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedWeaponModifierEffect
{
    public required string ComponentId { get; init; }

    public int SourceModifierIndex { get; init; } = -1;

    public string? ResolvedModifierId { get; init; }

    public bool IsExactlyResolved { get; init; }

    public bool IsLocal { get; init; }

    public bool HasProvenStatAssociation { get; init; }

    public bool UsesPositionalFallback { get; init; }

    public IReadOnlyList<string> ResolvedStatIds { get; init; } = [];

    public IReadOnlyList<decimal> CanonicalNumericValues { get; init; } = [];

    public ItemPropertySemanticDescriptor? ReviewedItemPropertySemantic { get; init; }
}

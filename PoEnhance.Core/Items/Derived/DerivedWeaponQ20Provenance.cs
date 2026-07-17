using PoEnhance.GameData;

namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedWeaponQ20Provenance
{
    public decimal? ObservedQuality { get; init; }

    public bool ObservedQualityAssumedZero { get; init; }

    public int NormalizedQuality { get; init; } = 20;

    public string? BaseItemId { get; init; }

    public ItemBaseWeaponProperties? BaseWeaponProperties { get; init; }

    public IReadOnlyList<DerivedWeaponQ20ModifierProvenance> ModifierContributions { get; init; } = [];

    public string? ReconstructionMethod { get; init; }

    public string? UnsupportedReason { get; init; }
}

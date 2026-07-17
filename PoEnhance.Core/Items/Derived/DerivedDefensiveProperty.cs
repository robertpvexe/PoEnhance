using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedDefensiveProperty
{
    public required ItemPropertyTarget Target { get; init; }
    public required decimal Value { get; init; }
    public required ParsedItemProperty SourceProperty { get; init; }
    public bool IsQ20 { get; init; }
    public string? UnsupportedReason { get; init; }
    public int? ReconstructedBaseValue { get; init; }
    public decimal LocalAdded { get; init; }
    public decimal LocalIncreasedPercent { get; init; }
    public decimal? ObservedQuality { get; init; }
    public ItemBaseDefenceProperties? BaseProperties { get; init; }
    public IReadOnlyList<DerivedWeaponQ20ModifierProvenance> ModifierContributions { get; init; } = [];
}

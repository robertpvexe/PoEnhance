namespace PoEnhance.Core.Items.Derived;

public sealed record DerivedDefensiveProperties
{
    public IReadOnlyList<DerivedDefensiveProperty> Properties { get; init; } = [];
}

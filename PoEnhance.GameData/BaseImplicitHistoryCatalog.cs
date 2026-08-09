namespace PoEnhance.GameData;

public sealed record BaseImplicitHistoryCatalog
{
    public IReadOnlyList<BaseImplicitSourceSnapshot> SourceSnapshots { get; init; } = [];

    public IReadOnlyList<BaseImplicitMechanicalEffect> MechanicalEffects { get; init; } = [];

    public IReadOnlyList<BaseImplicitObservation> Observations { get; init; } = [];
}

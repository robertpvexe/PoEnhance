namespace PoEnhance.GameData;

public sealed record UniqueItemCatalog
{
    public IReadOnlyList<UniqueCatalogSourceObservation> SourceObservations { get; init; } = [];

    public IReadOnlyList<UniqueItemIdentity> Items { get; init; } = [];
}

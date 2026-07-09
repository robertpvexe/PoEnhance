namespace PoEnhance.GameData;

public sealed record GameDataPackage
{
    public GameDataPackageManifest Manifest { get; init; } = new();

    public IReadOnlyList<ItemBaseRecord> ItemBases { get; init; } = [];

    public IReadOnlyList<ModifierDefinition> Modifiers { get; init; } = [];
}

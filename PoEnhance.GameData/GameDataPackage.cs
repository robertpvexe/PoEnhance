namespace PoEnhance.GameData;

public sealed record GameDataPackage
{
    public GameDataPackageManifest Manifest { get; init; } = new();

    public IReadOnlyList<ItemBaseRecord> ItemBases { get; init; } = [];

    public IReadOnlyList<ModifierDefinition> Modifiers { get; init; } = [];

    public IReadOnlyList<StatDefinition> Stats { get; init; } = [];

    public IReadOnlyList<StatTranslationDefinition> StatTranslations { get; init; } = [];

    public IReadOnlyList<ItemPropertySemanticDescriptor> ItemPropertySemantics { get; init; } = [];
}

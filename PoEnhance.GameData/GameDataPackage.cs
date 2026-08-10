using System.Text.Json.Serialization;

namespace PoEnhance.GameData;

public sealed record GameDataPackage
{
    public GameDataPackageManifest Manifest { get; init; } = new();

    public IReadOnlyList<ItemBaseRecord> ItemBases { get; init; } = [];

    public IReadOnlyList<ModifierDefinition> Modifiers { get; init; } = [];

    public IReadOnlyList<StatDefinition> Stats { get; init; } = [];

    public IReadOnlyList<StatTranslationDefinition> StatTranslations { get; init; } = [];

    public IReadOnlyList<ItemPropertySemanticDescriptor> ItemPropertySemantics { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ItemClassDefinition>? ItemClasses { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TagDefinition>? Tags { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BaseModifierSourceEvidence? BaseModifierEvidence { get; init; }

    /// <summary>
    /// Exact, versioned observations of base implicits. A null value means that the
    /// package carries no historical evidence; it is not evidence that no history exists.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BaseImplicitHistoryCatalog? BaseImplicitHistory { get; init; }

    /// <summary>
    /// Exact changed translation observations. Null means compatibility history is unavailable,
    /// not that no historical forms existed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StatTranslationHistoryCatalog? StatTranslationHistory { get; init; }
}

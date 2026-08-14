namespace PoEnhance.GameData;

/// <summary>
/// Explains a non-trivial Unique mechanics resolution without replacing copied values with
/// catalog rolls. Translation evidence is provider-neutral and comes from pinned GameData.
/// </summary>
public sealed record UniqueModifierMechanicalProvenance
{
    public IReadOnlyList<string> ResolutionReasons { get; init; } = [];

    public IReadOnlyList<UniqueModifierTranslationEvidence> Translations { get; init; } = [];

    public bool UsedComposition { get; init; }

    public bool CatalogValuesUsedForSelection { get; init; }

    public string ValueAuthority { get; init; } = "copiedInstance";

    public string? SafetyRationale { get; init; }
}

namespace PoEnhance.GameData;

public sealed record StatTranslationHistoryCatalog
{
    public IReadOnlyList<StatTranslationSourceSnapshot> SourceSnapshots { get; init; } = [];

    public IReadOnlyList<StatTranslationObservation> Observations { get; init; } = [];

    public IReadOnlyList<StatTranslationCompatibilityChange> Changes { get; init; } = [];
}

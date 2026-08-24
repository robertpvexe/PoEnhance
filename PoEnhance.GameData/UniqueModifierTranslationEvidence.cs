namespace PoEnhance.GameData;

/// <summary>One exact RePoE translation record and condition set used by a Unique mapping.</summary>
public sealed record UniqueModifierTranslationEvidence
{
    public string? TranslationId { get; init; }

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public IReadOnlyList<int> ModifierStatIndices { get; init; } = [];

    /// <summary>
    /// Translation-vector stats absent from the modifier record and therefore proven at the
    /// engine default of zero before the selected condition set was evaluated.
    /// </summary>
    public IReadOnlyList<string> DefaultedStatIds { get; init; } = [];

    public IReadOnlyList<StatTranslationCondition> Conditions { get; init; } = [];

    /// <summary>Translation value formats aligned to the translation stat vector.</summary>
    public IReadOnlyList<string> ValueFormats { get; init; } = [];

    public IReadOnlyList<string> FormatLines { get; init; } = [];

    /// <summary>
    /// RePoE translation handlers aligned to the translation stat vector. Their deterministic
    /// pipeline is retained as value-transform evidence; rendered values remain copied-instance
    /// data.
    /// </summary>
    public IReadOnlyList<StatTranslationIndexHandler> IndexHandlers { get; init; } = [];
}

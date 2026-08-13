using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed record UniqueModifierBlockResolution
{
    public required int ParsedModifierIndex { get; init; }

    public bool IsResolved { get; init; }

    public bool IsEquivalentSourceSet { get; init; }

    public IReadOnlyList<UniqueModifierBlock> CatalogBlocks { get; init; } = [];

    public IReadOnlyList<string> FoulbornRelationshipIds { get; init; } = [];

    public IReadOnlyList<string> NormalCounterpartModifierIds { get; init; } = [];

    public IReadOnlyList<string> ModifierIds { get; init; } = [];

    public IReadOnlyList<string> StatIds { get; init; } = [];

    /// <summary>
    /// Locality evidence aligned by index with <see cref="StatIds"/>. Unknown is retained
    /// when GameData cannot prove one side; callers must not infer it from text or item class.
    /// </summary>
    public IReadOnlyList<ModifierLocality> StatLocalities { get; init; } = [];

    public IReadOnlyList<string> CanonicalSignatures { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];

    /// <summary>
    /// Optional display-only lines after a source-proven generated annotation was removed.
    /// Raw parsed lines are retained separately by the Trade draft.
    /// </summary>
    public IReadOnlyList<string> PresentationLines { get; init; } = [];

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }
}

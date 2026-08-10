namespace PoEnhance.GameData;

/// <summary>One exact structured translation observed at one source snapshot.</summary>
public sealed record StatTranslationObservation
{
    public string? Id { get; init; }

    public string? SourceSnapshotId { get; init; }

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public StatTranslationDefinition? Translation { get; init; }

    public string? MechanicalSignature { get; init; }

    public string? RenderingSignature { get; init; }

    public string? NumericShapeSignature { get; init; }

    public int ModifierUsageCount { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

namespace PoEnhance.GameData;

public sealed record UniqueItemVersionObservation
{
    public string? Id { get; init; }

    public string? Label { get; init; }

    public UniqueItemVersionRole Role { get; init; }

    public string? BaseType { get; init; }

    public string? SourceBaseType { get; init; }

    public string? CanonicalBaseTypeKey { get; init; }

    public string? BaseTypeNormalizationRule { get; init; }

    public IReadOnlyList<string> RePoeBaseItemIds { get; init; } = [];

    public string? RoleDecisionReason { get; init; }

    public string? VariantDecisionReason { get; init; }

    /// <summary>
    /// Number of independently selected generated candidates observed in the source
    /// definition. Zero means the source did not expose a generated selection axis.
    /// </summary>
    public int GeneratedCandidateSelectionLimit { get; init; }

    /// <summary>
    /// Independently selectable source dimensions that coexist within this atomic version.
    /// Empty for packages or source definitions without explicit option-axis evidence.
    /// </summary>
    public IReadOnlyList<UniqueItemOptionAxis> OptionAxes { get; init; } = [];

    public IReadOnlyList<UniqueModifierBlock> ModifierBlocks { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}

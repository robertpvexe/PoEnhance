namespace PoEnhance.GameData;

/// <summary>
/// A compact reconstruction of one source modifier at one exact snapshot. The
/// structured modifier/stat/translation records are the equality evidence; display
/// text and the source modifier id are not sufficient on their own.
/// </summary>
public sealed record BaseImplicitMechanicalEffect
{
    public string? Id { get; init; }

    public string? SourceSnapshotId { get; init; }

    public string? SourceModifierId { get; init; }

    public bool IsResolved { get; init; }

    public string? MechanicalSignature { get; init; }

    public ModifierDefinition? Modifier { get; init; }

    public IReadOnlyList<StatDefinition> Stats { get; init; } = [];

    public IReadOnlyList<StatTranslationDefinition> StatTranslations { get; init; } = [];

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }
}

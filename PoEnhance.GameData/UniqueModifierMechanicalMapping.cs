namespace PoEnhance.GameData;

public sealed record UniqueModifierMechanicalMapping
{
    public UniqueModifierMechanicalMappingStatus Status { get; init; }

    public IReadOnlyList<string> ModifierIds { get; init; } = [];

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }
}

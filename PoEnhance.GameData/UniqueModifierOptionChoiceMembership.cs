namespace PoEnhance.GameData;

/// <summary>
/// Exact source membership of a modifier block in one independently selectable option choice.
/// </summary>
public sealed record UniqueModifierOptionChoiceMembership
{
    public string? OptionAxisId { get; init; }

    public string? OptionChoiceId { get; init; }

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}

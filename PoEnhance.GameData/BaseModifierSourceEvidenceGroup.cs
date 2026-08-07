namespace PoEnhance.GameData;

public sealed record BaseModifierSourceEvidenceGroup
{
    public IReadOnlyList<string> BaseItemIds { get; init; } = [];

    public IReadOnlyList<BaseModifierSourceEvidenceEntry> Modifiers { get; init; } = [];

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}

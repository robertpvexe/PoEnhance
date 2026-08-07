namespace PoEnhance.GameData;

public sealed record BaseModifierSourceEvidence
{
    public BaseModifierEvidenceSemantics Semantics { get; init; }

    public BaseModifierEvidenceCoverage Coverage { get; init; }

    public int SourceBaseEntriesRead { get; init; }

    public int BaseEntriesRepresented { get; init; }

    public int BaseEntriesUnavailable { get; init; }

    public int SourceRelationshipsRead { get; init; }

    public int RelationshipsRepresented { get; init; }

    public int RelationshipsUnavailableBases { get; init; }

    public int RelationshipsUnavailableStatlessModifiers { get; init; }

    public int RelationshipsUnavailableOtherModifiers { get; init; }

    public int RelationshipsUnresolved { get; init; }

    public int SpecialSourceEntriesNotModeled { get; init; }

    public IReadOnlyList<BaseModifierSourceEvidenceGroup> Groups { get; init; } = [];

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}

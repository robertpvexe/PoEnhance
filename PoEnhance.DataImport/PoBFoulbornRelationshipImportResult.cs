using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed record PoBFoulbornRelationshipImportResult
{
    public UniqueFoulbornRelationshipSourceObservation? SourceObservation { get; init; }

    public IReadOnlyList<UniqueFoulbornModifierRelationship> Relationships { get; init; } = [];

    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];

    public int ItemRecordsRead { get; init; }

    public int RelationshipsRead { get; init; }

    public int RelationshipsLinked { get; init; }

    public int RelationshipsUnsupported { get; init; }
}

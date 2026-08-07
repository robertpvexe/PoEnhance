namespace PoEnhance.DataImport;

public sealed record RePoeModsByBaseImportAudit
{
    public int SourceGroupsRead { get; init; }

    public int GroupsImported { get; init; }

    public int SpecialSourceEntriesNotModeled { get; init; }

    public int SourceBaseEntriesRead { get; init; }

    public int BaseEntriesImported { get; init; }

    public int BaseEntriesSkipped { get; init; }

    public int DuplicateBaseEntries { get; init; }

    public int SourceRelationshipsRead { get; init; }

    public int RelationshipsImported { get; init; }

    public int DuplicateRelationships { get; init; }

    public int UnknownBaseReferences { get; init; }

    public int RelationshipsUnavailableBases { get; init; }

    public int RelationshipsUnavailableStatlessModifiers { get; init; }

    public int RelationshipsUnavailableOtherModifiers { get; init; }

    public int UnknownModifierRelationships { get; init; }

    public int MalformedRelationships { get; init; }

    public int UnresolvedRelationships => RelationshipsUnavailableOtherModifiers +
        UnknownModifierRelationships +
        MalformedRelationships;

    public IReadOnlyDictionary<string, int> SourceGenerationRelationshipCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SourceGenerationBucketRelationshipCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
}

namespace PoEnhance.DataImport;

public sealed record GameDataPackageBuildRecordCounts
{
    public int ItemBases { get; init; }

    public int Modifiers { get; init; }

    public int Stats { get; init; }

    public int StatTranslations { get; init; }

    public int ItemPropertySemantics { get; init; }

    public int ItemClasses { get; init; }

    public int Tags { get; init; }

    public int BaseModifierEvidenceGroups { get; init; }

    public int BaseModifierRelationships { get; init; }

    public int UniqueItems { get; init; }

    public int UniqueVersions { get; init; }

    public int UniqueModifierBlocks { get; init; }
}

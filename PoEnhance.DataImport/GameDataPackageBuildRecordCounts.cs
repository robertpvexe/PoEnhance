namespace PoEnhance.DataImport;

public sealed record GameDataPackageBuildRecordCounts
{
    public int ItemBases { get; init; }

    public int Modifiers { get; init; }

    public int Stats { get; init; }

    public int StatTranslations { get; init; }
}

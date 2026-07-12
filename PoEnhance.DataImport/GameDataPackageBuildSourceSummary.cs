namespace PoEnhance.DataImport;

public sealed record GameDataPackageBuildSourceSummary
{
    public string SourceName { get; init; } = string.Empty;

    public int SourceRecordsRead { get; init; }

    public int RecordsImported { get; init; }

    public int RecordsSkipped { get; init; }
}

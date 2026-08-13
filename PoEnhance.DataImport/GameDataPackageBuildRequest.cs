namespace PoEnhance.DataImport;

public sealed record GameDataPackageBuildRequest
{
    public string? BaseItemsPath { get; init; }

    public string? ModsPath { get; init; }

    public string? StatsPath { get; init; }

    public string? TranslationsPath { get; init; }

    public string? ItemClassesPath { get; init; }

    public string? TagsPath { get; init; }

    public string? ModsByBasePath { get; init; }

    public string? ItemPropertySemanticsPath { get; init; }

    public string? OutputPath { get; init; }

    public string? SourceSnapshotDirectory { get; init; }

    public string? SourceRootPath { get; init; }

    public string? SourceDataRootPath { get; init; }

    public string? SourceUri { get; init; }

    public string? SourceBranch { get; init; }

    public string? DataVersion { get; init; }

    public string? League { get; init; }

    public string? Patch { get; init; }

    public string? SourceVersion { get; init; }

    public string? HistoricalBaseItemsPath { get; init; }

    public string? HistoricalModsPath { get; init; }

    public string? HistoricalStatsPath { get; init; }

    public string? HistoricalTranslationsPath { get; init; }

    public string? HistoricalSourceRootPath { get; init; }

    public string? HistoricalSourceDataRootPath { get; init; }

    public string? HistoricalSourceUri { get; init; }

    public string? HistoricalSourceBranch { get; init; }

    public string? HistoricalSourceVersion { get; init; }

    public string? HistoricalDataVersion { get; init; }

    public string? PoBUniquesPath { get; init; }

    public string? PoBFoulbornMapPath { get; init; }

    public string? PoBSourceRootPath { get; init; }

    public string? PoBSourceUri { get; init; }

    public string? PoBSourceTag { get; init; }

    public string? PoBSourceVersion { get; init; }

    public DateTimeOffset? CreatedAtUtc { get; init; }
}

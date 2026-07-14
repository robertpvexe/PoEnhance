namespace PoEnhance.DataImport;

public sealed record GameDataPackageBuildRequest
{
    public string? BaseItemsPath { get; init; }

    public string? ModsPath { get; init; }

    public string? StatsPath { get; init; }

    public string? TranslationsPath { get; init; }

    public string? OutputPath { get; init; }

    public string? SourceRootPath { get; init; }

    public string? SourceDataRootPath { get; init; }

    public string? SourceUri { get; init; }

    public string? SourceBranch { get; init; }

    public string? DataVersion { get; init; }

    public string? League { get; init; }

    public string? Patch { get; init; }

    public string? SourceVersion { get; init; }

    public DateTimeOffset? CreatedAtUtc { get; init; }
}

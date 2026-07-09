namespace PoEnhance.GameData;

public sealed record GameDataSourceReference
{
    public string? SourceId { get; init; }

    public string? ExternalId { get; init; }

    public string? ExternalUri { get; init; }
}

using System.Text.Json.Serialization;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSearchRequest
{
    [JsonPropertyOrder(0)]
    public required PathOfExileTradeSearchQuery Query { get; init; }

    [JsonPropertyOrder(1)]
    public required PathOfExileTradeSearchSort Sort { get; init; }
}

internal sealed record PathOfExileTradeSearchQuery
{
    [JsonPropertyOrder(0)]
    public required PathOfExileTradeSearchStatus Status { get; init; }

    [JsonPropertyOrder(1)]
    public string? Name { get; init; }

    [JsonPropertyOrder(2)]
    public required string Type { get; init; }

    [JsonPropertyOrder(3)]
    public IReadOnlyList<PathOfExileTradeSearchStatsGroup> Stats { get; init; } =
    [
        new PathOfExileTradeSearchStatsGroup(),
    ];

    [JsonPropertyOrder(4)]
    public IReadOnlyDictionary<string, object> Filters { get; init; } =
        new Dictionary<string, object>();
}

internal sealed record PathOfExileTradeSearchStatus
{
    public required string Option { get; init; }
}

internal sealed record PathOfExileTradeSearchStatsGroup
{
    [JsonPropertyOrder(0)]
    public string Type { get; init; } = "and";

    [JsonPropertyOrder(1)]
    public IReadOnlyList<object> Filters { get; init; } = [];
}

internal sealed record PathOfExileTradeSearchSort
{
    public string Price { get; init; } = "asc";
}

using PoEnhance.Core.Items.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeQueryBuildResult
{
    public bool IsSuccess => Request is not null;

    public string? LeagueIdentifier { get; init; }

    public PathOfExileTradeSearchRequest? Request { get; init; }

    public string? SerializedJson { get; init; }

    public string? SelectedBaseType { get; init; }

    public ItemBaseResolutionStatus? SelectedBaseResolutionStatus { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeQueryBuildResult Success(
        string leagueIdentifier,
        PathOfExileTradeSearchRequest request,
        string serializedJson,
        string? selectedBaseType,
        ItemBaseResolutionStatus? selectedBaseResolutionStatus)
    {
        return new PathOfExileTradeQueryBuildResult
        {
            LeagueIdentifier = leagueIdentifier,
            Request = request,
            SerializedJson = serializedJson,
            SelectedBaseType = selectedBaseType,
            SelectedBaseResolutionStatus = selectedBaseResolutionStatus,
        };
    }

    public static PathOfExileTradeQueryBuildResult Failure(
        params PathOfExileTradeQueryDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeQueryBuildResult
        {
            Diagnostics = diagnostics,
        };
    }
}

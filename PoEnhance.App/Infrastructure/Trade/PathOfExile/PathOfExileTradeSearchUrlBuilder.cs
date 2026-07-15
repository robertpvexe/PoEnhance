namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeSearchUrlBuilder
{
    private const string TradeSearchBaseUrl = "https://www.pathofexile.com/trade/search";

    public bool TryBuild(
        string? leagueIdentifier,
        string? searchQueryId,
        out Uri? uri)
    {
        var league = TrimToNull(leagueIdentifier);
        var queryId = TrimToNull(searchQueryId);
        if (league is null || queryId is null)
        {
            uri = null;
            return false;
        }

        return Uri.TryCreate(
            $"{TradeSearchBaseUrl}/{Uri.EscapeDataString(league)}/{Uri.EscapeDataString(queryId)}",
            UriKind.Absolute,
            out uri);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

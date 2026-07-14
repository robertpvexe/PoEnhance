namespace PoEnhance.App.Features.PriceChecking;

internal sealed class NullPriceCheckerLeaguePreferenceStore : IPriceCheckerLeaguePreferenceStore
{
    private NullPriceCheckerLeaguePreferenceStore()
    {
    }

    public static NullPriceCheckerLeaguePreferenceStore Instance { get; } = new();

    public string? LoadLeagueIdentifier()
    {
        return null;
    }

    public void SaveLeagueIdentifier(string leagueIdentifier)
    {
    }
}

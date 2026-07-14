namespace PoEnhance.App.Features.PriceChecking;

internal interface IPriceCheckerLeaguePreferenceStore
{
    string? LoadLeagueIdentifier();

    void SaveLeagueIdentifier(string leagueIdentifier);
}

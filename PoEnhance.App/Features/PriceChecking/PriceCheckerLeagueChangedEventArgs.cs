namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerLeagueChangedEventArgs : EventArgs
{
    public PriceCheckerLeagueChangedEventArgs(string? leagueIdentifier)
    {
        LeagueIdentifier = leagueIdentifier;
    }

    public string? LeagueIdentifier { get; }
}

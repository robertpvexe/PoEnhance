using System.IO;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerLeaguePreferenceStorePathResolver
{
    private readonly Func<Environment.SpecialFolder, string> getFolderPath;

    public PriceCheckerLeaguePreferenceStorePathResolver()
        : this(Environment.GetFolderPath)
    {
    }

    public PriceCheckerLeaguePreferenceStorePathResolver(
        Func<Environment.SpecialFolder, string> getFolderPath)
    {
        this.getFolderPath = getFolderPath;
    }

    public string ResolveDefaultPath()
    {
        return Path.Combine(
            getFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoEnhance",
            "price-checker-league.json");
    }
}

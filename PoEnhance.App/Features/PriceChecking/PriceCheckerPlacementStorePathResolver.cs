using System.IO;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerPlacementStorePathResolver
{
    private readonly Func<Environment.SpecialFolder, string> getFolderPath;

    public PriceCheckerPlacementStorePathResolver()
        : this(Environment.GetFolderPath)
    {
    }

    public PriceCheckerPlacementStorePathResolver(
        Func<Environment.SpecialFolder, string> getFolderPath)
    {
        this.getFolderPath = getFolderPath;
    }

    public string ResolveDefaultPath()
    {
        return Path.Combine(
            getFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoEnhance",
            "price-checker-placement.json");
    }
}

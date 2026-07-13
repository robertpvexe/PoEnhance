using System.IO;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed class ProvisionalGameDataStorePathResolver
{
    public const string DirectoryName = "PoEnhance";
    public const string FileName = "provisional-game-data.json";

    private readonly Func<Environment.SpecialFolder, string> getFolderPath;

    public ProvisionalGameDataStorePathResolver()
        : this(Environment.GetFolderPath)
    {
    }

    public ProvisionalGameDataStorePathResolver(Func<Environment.SpecialFolder, string> getFolderPath)
    {
        this.getFolderPath = getFolderPath;
    }

    public string ResolveDefaultPath()
    {
        var localAppData = getFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, DirectoryName, FileName);
    }
}

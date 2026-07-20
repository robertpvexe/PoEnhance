using System.IO;

namespace PoEnhance.App.Infrastructure.GameData;

internal class GameDataPackagePathResolver
{
    public const string CommandLineOption = "--game-data";
    public const string EnvironmentVariableName = "POENHANCE_GAMEDATA_PATH";
    public const string PackagedFileName = "poenhance-game-data.json";
    public const string DevelopmentFallbackRelativePath = "artifacts/poenhance-game-data.json";

    private readonly Func<string, string?> getEnvironmentVariable;
    private readonly Func<string> getBaseDirectory;
    private readonly Func<string, bool> fileExists;

    public GameDataPackagePathResolver()
        : this(Environment.GetEnvironmentVariable, () => AppContext.BaseDirectory, File.Exists)
    {
    }

    public GameDataPackagePathResolver(
        Func<string, string?> getEnvironmentVariable,
        Func<string> getBaseDirectory,
        Func<string, bool> fileExists)
    {
        this.getEnvironmentVariable = getEnvironmentVariable;
        this.getBaseDirectory = getBaseDirectory;
        this.fileExists = fileExists;
    }

    public virtual GameDataPackagePathResolution Resolve(IReadOnlyList<string> commandLineArgs)
    {
        var baseDirectory = GetFullPathOrNull(getBaseDirectory());
        if (baseDirectory is not null)
        {
            var packagedPath = System.IO.Path.Combine(baseDirectory, PackagedFileName);
            if (fileExists(packagedPath))
            {
                return new GameDataPackagePathResolution(
                    System.IO.Path.GetFullPath(packagedPath),
                    GameDataPackagePathSource.Packaged);
            }
        }

        var commandLinePath = ReadCommandLinePath(commandLineArgs);
        if (!string.IsNullOrWhiteSpace(commandLinePath))
        {
            return new GameDataPackagePathResolution(commandLinePath, GameDataPackagePathSource.CommandLine);
        }

        var environmentPath = getEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            return new GameDataPackagePathResolution(environmentPath, GameDataPackagePathSource.Environment);
        }

        var fallbackPath = FindDevelopmentFallback(baseDirectory);
        return string.IsNullOrWhiteSpace(fallbackPath)
            ? new GameDataPackagePathResolution(null, GameDataPackagePathSource.None)
            : new GameDataPackagePathResolution(fallbackPath, GameDataPackagePathSource.DevelopmentFallback);
    }

    private static string? ReadCommandLinePath(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, CommandLineOption, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1 < args.Count ? args[index + 1] : null;
            }

            var equalsPrefix = $"{CommandLineOption}=";
            if (arg.StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg[equalsPrefix.Length..];
            }
        }

        return null;
    }

    private string? FindDevelopmentFallback(string? baseDirectory)
    {
        if (baseDirectory is null)
        {
            return null;
        }

        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null)
        {
            var candidatePath = System.IO.Path.Combine(
                directory.FullName,
                DevelopmentFallbackRelativePath);
            if (fileExists(candidatePath))
            {
                return System.IO.Path.GetFullPath(candidatePath);
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? GetFullPathOrNull(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}

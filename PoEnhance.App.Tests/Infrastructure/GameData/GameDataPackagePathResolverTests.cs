using PoEnhance.App.Infrastructure.GameData;

namespace PoEnhance.App.Tests.Infrastructure.GameData;

public sealed class GameDataPackagePathResolverTests
{
    [Fact]
    public void Resolve_CommandLinePathTakesPriority()
    {
        var resolver = CreateResolver(
            environmentPath: "env-package.json",
            currentDirectory: @"C:\repo",
            existingFiles: [@"C:\repo\artifacts\poenhance-game-data.json"]);

        var result = resolver.Resolve(["--game-data", "cli-package.json"]);

        Assert.Equal(GameDataPackagePathSource.CommandLine, result.Source);
        Assert.Equal("cli-package.json", result.Path);
    }

    [Fact]
    public void Resolve_CommandLineEqualsSyntaxTakesPriority()
    {
        var resolver = CreateResolver(
            environmentPath: "env-package.json",
            currentDirectory: @"C:\repo",
            existingFiles: [@"C:\repo\artifacts\poenhance-game-data.json"]);

        var result = resolver.Resolve(["--game-data=cli-package.json"]);

        Assert.Equal(GameDataPackagePathSource.CommandLine, result.Source);
        Assert.Equal("cli-package.json", result.Path);
    }

    [Fact]
    public void Resolve_EnvironmentPathIsUsedWhenCommandLinePathIsMissing()
    {
        var resolver = CreateResolver(
            environmentPath: "env-package.json",
            currentDirectory: @"C:\repo",
            existingFiles: [@"C:\repo\artifacts\poenhance-game-data.json"]);

        var result = resolver.Resolve([]);

        Assert.Equal(GameDataPackagePathSource.Environment, result.Source);
        Assert.Equal("env-package.json", result.Path);
    }

    [Fact]
    public void Resolve_DevelopmentFallbackIsUsedWhenAvailable()
    {
        var currentDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "src", "PoEnhance.App");
        var repoRoot = Directory.GetParent(currentDirectory)!.Parent!.FullName;
        var fallbackPath = Path.Combine(repoRoot, "artifacts", "poenhance-game-data.json");
        var resolver = CreateResolver(
            environmentPath: null,
            currentDirectory: currentDirectory,
            existingFiles: [fallbackPath]);

        var result = resolver.Resolve([]);

        Assert.Equal(GameDataPackagePathSource.DevelopmentFallback, result.Source);
        Assert.Equal(fallbackPath, result.Path);
    }

    [Fact]
    public void Resolve_MissingPackageProducesNotConfigured()
    {
        var resolver = CreateResolver(
            environmentPath: null,
            currentDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            existingFiles: []);

        var result = resolver.Resolve([]);

        Assert.False(result.IsConfigured);
        Assert.Equal(GameDataPackagePathSource.None, result.Source);
        Assert.Null(result.Path);
    }

    private static GameDataPackagePathResolver CreateResolver(
        string? environmentPath,
        string currentDirectory,
        IReadOnlyCollection<string> existingFiles)
    {
        var normalizedFiles = new HashSet<string>(
            existingFiles.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);

        return new GameDataPackagePathResolver(
            name => name == GameDataPackagePathResolver.EnvironmentVariableName ? environmentPath : null,
            () => currentDirectory,
            path => normalizedFiles.Contains(Path.GetFullPath(path)));
    }
}

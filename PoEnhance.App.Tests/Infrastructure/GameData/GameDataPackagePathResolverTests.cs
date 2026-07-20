using PoEnhance.App.Infrastructure.GameData;

namespace PoEnhance.App.Tests.Infrastructure.GameData;

public sealed class GameDataPackagePathResolverTests
{
    [Fact]
    public void Resolve_PackagedFileBesideExecutableTakesPriority()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(root, "release");
        var packagedPath = Path.Combine(baseDirectory, GameDataPackagePathResolver.PackagedFileName);
        var developmentPath = Path.Combine(root, "artifacts", "poenhance-game-data.json");
        var resolver = CreateResolver(
            environmentPath: "environment-package.json",
            baseDirectory,
            existingFiles: [packagedPath, developmentPath]);

        var result = resolver.Resolve(["--game-data", "command-line-package.json"]);

        Assert.Equal(GameDataPackagePathSource.Packaged, result.Source);
        Assert.Equal(Path.GetFullPath(packagedPath), result.Path);
    }

    [Fact]
    public void Resolve_CommandLinePathIsAvailableWhenPackagedFileIsMissing()
    {
        var resolver = CreateResolver(
            environmentPath: "env-package.json",
            baseDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            existingFiles: []);

        var result = resolver.Resolve(["--game-data", "cli-package.json"]);

        Assert.Equal(GameDataPackagePathSource.CommandLine, result.Source);
        Assert.Equal("cli-package.json", result.Path);
    }

    [Fact]
    public void Resolve_CommandLineEqualsSyntaxIsAvailableWhenPackagedFileIsMissing()
    {
        var resolver = CreateResolver(
            environmentPath: "env-package.json",
            baseDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            existingFiles: []);

        var result = resolver.Resolve(["--game-data=cli-package.json"]);

        Assert.Equal(GameDataPackagePathSource.CommandLine, result.Source);
        Assert.Equal("cli-package.json", result.Path);
    }

    [Fact]
    public void Resolve_EnvironmentPathIsAvailableWhenPackagedAndCommandLinePathsAreMissing()
    {
        var resolver = CreateResolver(
            environmentPath: "env-package.json",
            baseDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            existingFiles: []);

        var result = resolver.Resolve([]);

        Assert.Equal(GameDataPackagePathSource.Environment, result.Source);
        Assert.Equal("env-package.json", result.Path);
    }

    [Fact]
    public void Resolve_DevelopmentFallbackWalksFromApplicationBaseDirectory()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "repo");
        var baseDirectory = Path.Combine(repoRoot, "PoEnhance.App", "bin", "Debug", "net10.0-windows");
        var fallbackPath = Path.Combine(repoRoot, "artifacts", "poenhance-game-data.json");
        var resolver = CreateResolver(
            environmentPath: null,
            baseDirectory,
            existingFiles: [fallbackPath]);

        var result = resolver.Resolve([]);

        Assert.Equal(GameDataPackagePathSource.DevelopmentFallback, result.Source);
        Assert.Equal(Path.GetFullPath(fallbackPath), result.Path);
    }

    [Fact]
    public void Resolve_UnrelatedWorkingDirectoryCannotRedirectPackagedResolution()
    {
        var releaseRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "release");
        var packagedPath = Path.Combine(releaseRoot, GameDataPackagePathResolver.PackagedFileName);
        var unrelatedDevelopmentPath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            "artifacts",
            "poenhance-game-data.json");
        var resolver = CreateResolver(
            environmentPath: null,
            baseDirectory: releaseRoot,
            existingFiles: [packagedPath, unrelatedDevelopmentPath]);

        var result = resolver.Resolve([]);

        Assert.Equal(GameDataPackagePathSource.Packaged, result.Source);
        Assert.Equal(Path.GetFullPath(packagedPath), result.Path);
    }

    [Fact]
    public void Resolve_MissingPackagedAndDevelopmentFilesProducesNotConfigured()
    {
        var resolver = CreateResolver(
            environmentPath: null,
            baseDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            existingFiles: []);

        var result = resolver.Resolve([]);

        Assert.False(result.IsConfigured);
        Assert.Equal(GameDataPackagePathSource.None, result.Source);
        Assert.Null(result.Path);
    }

    private static GameDataPackagePathResolver CreateResolver(
        string? environmentPath,
        string baseDirectory,
        IReadOnlyCollection<string> existingFiles)
    {
        var normalizedFiles = new HashSet<string>(
            existingFiles.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);

        return new GameDataPackagePathResolver(
            name => name == GameDataPackagePathResolver.EnvironmentVariableName ? environmentPath : null,
            () => baseDirectory,
            path => normalizedFiles.Contains(Path.GetFullPath(path)));
    }
}

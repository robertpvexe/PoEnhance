using System.Diagnostics;
using System.Reflection;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;

namespace PoEnhance.App.Tests;

public sealed class ReleasePackagingTests
{
    [Fact]
    public void ApplicationAssembly_ExposesV010ReleaseMetadata()
    {
        var assembly = typeof(App).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);

        Assert.Equal("PoEnhance.App", assembly.GetName().Name);
        Assert.Equal(new Version(0, 1, 0, 0), assembly.GetName().Version);
        Assert.Equal("0.1.0", informationalVersion);
        Assert.Equal("0.1.0.0", fileVersion.FileVersion);
        Assert.Equal("PoEnhance", fileVersion.ProductName);
    }

    [Fact]
    public void ApplicationProject_DeclaresStandaloneFolderPublishAndLinkedGameData()
    {
        var project = ReadRepositoryFile("PoEnhance.App", "PoEnhance.App.csproj");

        Assert.Contains("<Version>0.1.0</Version>", project, StringComparison.Ordinal);
        Assert.Contains("<PublishTrimmed>false</PublishTrimmed>", project, StringComparison.Ordinal);
        Assert.Contains("<PublishSingleFile>false</PublishSingleFile>", project, StringComparison.Ordinal);
        Assert.Contains("..\\artifacts\\poenhance-game-data.json", project, StringComparison.Ordinal);
        Assert.Contains("Link=\"poenhance-game-data.json\"", project, StringComparison.Ordinal);
        Assert.Contains("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>", project, StringComparison.Ordinal);
        Assert.Contains("<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishScript_UsesConstrainedStagingSelfContainedPublishAndChecksums()
    {
        var script = ReadRepositoryFile("scripts", "Publish-PoEnhance.ps1");

        Assert.Contains("[string]$Version", script, StringComparison.Ordinal);
        Assert.Contains("[string]$OutputRoot", script, StringComparison.Ordinal);
        Assert.Contains("--runtime', 'win-x64'", script, StringComparison.Ordinal);
        Assert.Contains("--self-contained', 'true'", script, StringComparison.Ordinal);
        Assert.Contains("-p:PublishTrimmed=false", script, StringComparison.Ordinal);
        Assert.Contains("-p:PublishSingleFile=false", script, StringComparison.Ordinal);
        Assert.Contains("Compress-Archive", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("Assert-DirectChildPath", script, StringComparison.Ordinal);
        Assert.Contains("Exit PoEnhance from its tray menu", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.Exit", script, StringComparison.Ordinal);
        Assert.DoesNotContain("%LocalAppData%", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MutableStores_RemainUnderUserLocalApplicationData()
    {
        const string localApplicationData = @"C:\Users\ReleaseTest\AppData\Local";
        var provisionalPath = new ProvisionalGameDataStorePathResolver(
            folder => folder == Environment.SpecialFolder.LocalApplicationData
                ? localApplicationData
                : throw new InvalidOperationException()).ResolveDefaultPath();
        var placementPath = new PriceCheckerPlacementStorePathResolver(
            folder => folder == Environment.SpecialFolder.LocalApplicationData
                ? localApplicationData
                : throw new InvalidOperationException()).ResolveDefaultPath();

        Assert.Equal(
            Path.Combine(localApplicationData, "PoEnhance", "provisional-game-data.json"),
            provisionalPath);
        Assert.Equal(
            Path.Combine(localApplicationData, "PoEnhance", "price-checker-placement.json"),
            placementPath);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. pathParts]));
    }
}

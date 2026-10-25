using System.Security.Cryptography;
using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// Authoritative active GameData pin from <c>data/game-data/sources.json</c> plus the
/// on-disk <c>artifacts/poenhance-game-data.json</c> identity. Tests must not hardcode a
/// historical dataVersion string when they mean "current pin".
/// </summary>
internal static class PinnedActiveGameDataPackage
{
    public static string ResolvePackagePath()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(path), $"Active GameData package missing: {path}");
        return path;
    }

    public static string ResolveSourcesPath()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "data", "game-data", "sources.json"));
        Assert.True(File.Exists(path), $"Pinned sources.json missing: {path}");
        return path;
    }

    public static (int SchemaVersion, string DataVersion, long SizeBytes, string Sha256) ReadPin()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ResolveSourcesPath()));
        var package = document.RootElement.GetProperty("package");
        var schemaVersion = package.GetProperty("schemaVersion").GetInt32();
        var dataVersion = package.GetProperty("dataVersion").GetString();
        var sizeBytes = package.GetProperty("sizeBytes").GetInt64();
        var sha256 = package.GetProperty("sha256").GetString();
        Assert.True(schemaVersion > 0);
        Assert.False(string.IsNullOrWhiteSpace(dataVersion));
        Assert.True(sizeBytes > 0);
        Assert.False(string.IsNullOrWhiteSpace(sha256));
        return (schemaVersion, dataVersion!, sizeBytes, sha256!);
    }

    public static async Task<GameDataPackage> LoadAndAssertMatchesPinAsync()
    {
        var packagePath = ResolvePackagePath();
        var pin = ReadPin();
        var fileInfo = new FileInfo(packagePath);
        Assert.Equal(pin.SizeBytes, fileInfo.Length);
        Assert.Equal(pin.Sha256, ComputeSha256Hex(packagePath), ignoreCase: true);

        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join(", ", load.Diagnostics.Select(d => d.Code)));
        Assert.NotNull(load.Package);
        Assert.Equal(pin.SchemaVersion, load.Package.Manifest.SchemaVersion);
        Assert.Equal(pin.DataVersion, load.Package.Manifest.DataVersion);
        return load.Package;
    }

    public static async Task<GameDataCatalog> LoadCatalogAndAssertMatchesPinAsync() =>
        GameDataCatalog.FromPackage(await LoadAndAssertMatchesPinAsync());

    private static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class GameDataPackageManifestJsonTests
{
    [Fact]
    public void Serialize_UsesCamelCaseReadableJson()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest();

        var json = GameDataPackageManifestJson.Serialize(manifest);

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"dataVersion\": \"dev-2026-01-15\"", json);
        Assert.Contains("\"sourceId\": \"repoe\"", json);
        Assert.Contains("\n", json);
        Assert.Contains("  \"sources\": [", json);
    }

    [Fact]
    public void Deserialize_SerializedManifest_RoundTrips()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest();

        var json = GameDataPackageManifestJson.Serialize(manifest);
        var roundTrippedManifest = GameDataPackageManifestJson.Deserialize(json);

        Assert.NotNull(roundTrippedManifest);
        Assert.Equal(manifest.SchemaVersion, roundTrippedManifest.SchemaVersion);
        Assert.Equal(manifest.DataVersion, roundTrippedManifest.DataVersion);
        Assert.Equal(manifest.CreatedAtUtc, roundTrippedManifest.CreatedAtUtc);
        Assert.Equal(manifest.League, roundTrippedManifest.League);
        Assert.Equal(manifest.Patch, roundTrippedManifest.Patch);
        Assert.Equal(manifest.Sources, roundTrippedManifest.Sources);
    }

    [Fact]
    public void Deserialize_SerializedManifest_PreservesSourceMetadataExactly()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest();

        var json = GameDataPackageManifestJson.Serialize(manifest);
        var roundTrippedManifest = GameDataPackageManifestJson.Deserialize(json);

        Assert.NotNull(roundTrippedManifest);
        Assert.Equal(2, roundTrippedManifest.Sources.Count);

        var repoe = roundTrippedManifest.Sources[0];
        Assert.Equal("repoe", repoe.SourceId);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 12, 5, 0, TimeSpan.Zero), repoe.RetrievedAtUtc);
        Assert.Equal("repoe-dev-snapshot", repoe.SourceVersion);
        Assert.Equal("https://github.com/brather1ng/RePoE", repoe.SourceUri);

        var poedb = roundTrippedManifest.Sources[1];
        Assert.Equal("poedb", poedb.SourceId);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 12, 10, 0, TimeSpan.Zero), poedb.RetrievedAtUtc);
        Assert.Equal("poedb-dev-snapshot", poedb.SourceVersion);
        Assert.Equal("https://poedb.tw", poedb.SourceUri);
    }
}

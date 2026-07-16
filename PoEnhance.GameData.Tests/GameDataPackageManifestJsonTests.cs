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
        Assert.Contains("\"sourceBranch\": \"master\"", json);
        Assert.Contains("\"inputFiles\": [", json);
        Assert.Contains("\"reviewedItemPropertySemantics\": {", json);
        Assert.Contains("\"reviewVersion\": \"weapon-dps-v1\"", json);
        Assert.Contains("\"itemPropertySemanticAugmentation\": {", json);
        Assert.Contains("\"operationId\": \"augment-package-semantics\"", json);
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
        Assert.Equal(manifest.ReviewedItemPropertySemantics, roundTrippedManifest.ReviewedItemPropertySemantics);
        Assert.Equal(manifest.ItemPropertySemanticAugmentation, roundTrippedManifest.ItemPropertySemanticAugmentation);
        Assert.Equal(manifest.Sources.Count, roundTrippedManifest.Sources.Count);
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
        Assert.Equal("c50acab2ed660a70511e7f91ee09db4e632089e4", repoe.SourceVersion);
        Assert.Equal("https://github.com/repoe-fork/repoe", repoe.SourceUri);
        Assert.Equal("master", repoe.SourceBranch);
        Assert.Equal("/sources/repoe-fork", repoe.SourceRoot);
        Assert.Equal("/sources/active-poe1", repoe.SourceDataRoot);
        Assert.Equal(2, repoe.InputFiles.Count);
        var baseItems = repoe.InputFiles[0];
        Assert.Equal("base_items.json", baseItems.Label);
        Assert.Equal("base_items.json", baseItems.RelativePath);
        Assert.Equal(1_024, baseItems.SizeBytes);
        Assert.Equal("96669bd7d4d7552e8cb2f15ee5fd0173580c7b14ca17583f55645b275a4d6ad1", baseItems.Sha256);

        var poedb = roundTrippedManifest.Sources[1];
        Assert.Equal("poedb", poedb.SourceId);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 12, 10, 0, TimeSpan.Zero), poedb.RetrievedAtUtc);
        Assert.Equal("poedb-dev-snapshot", poedb.SourceVersion);
        Assert.Equal("https://poedb.tw", poedb.SourceUri);

        var augmentation = Assert.IsType<GameDataPackageItemPropertySemanticAugmentation>(
            roundTrippedManifest.ItemPropertySemanticAugmentation);
        Assert.Equal("augment-package-semantics", augmentation.OperationId);
        Assert.Equal("input-package", augmentation.InputPackageLabel);
        Assert.Equal("poenhance-game-data.json", augmentation.InputPackageDisplayPath);
        Assert.Equal(64_000_000, augmentation.InputPackageSizeBytes);
        Assert.Equal("d3020e21620b72377673b7e2307f99ba0f6cae1000372d8dc2cdb6e88fe93740", augmentation.InputPackageSha256);
        Assert.Equal("dev-2026-01-15-base", augmentation.InputPackageDataVersion);
    }

    [Fact]
    public void Deserialize_OldManifestWithoutReviewedSemanticProvenance_RemainsCompatible()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "dataVersion": "old-package",
              "createdAtUtc": "2026-01-15T12:00:00+00:00",
              "sources": [
                {
                  "sourceId": "repoe",
                  "retrievedAtUtc": "2026-01-15T12:05:00+00:00"
                }
              ]
            }
            """;

        var manifest = GameDataPackageManifestJson.Deserialize(json);

        Assert.NotNull(manifest);
        Assert.Null(manifest.ReviewedItemPropertySemantics);
        Assert.Null(manifest.ItemPropertySemanticAugmentation);
        Assert.True(GameDataPackageManifestValidator.Validate(manifest).IsValid);
    }

    [Fact]
    public void Serialize_ReviewedSemanticFingerprint_IsDeterministic()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest();

        var first = GameDataPackageManifestJson.Serialize(manifest);
        var second = GameDataPackageManifestJson.Serialize(manifest);

        Assert.Equal(first, second);
        Assert.True(
            first.IndexOf("\"label\"", StringComparison.Ordinal) <
            first.IndexOf("\"displayPath\"", StringComparison.Ordinal));
        Assert.True(
            first.IndexOf("\"sizeBytes\"", StringComparison.Ordinal) <
            first.IndexOf("\"sha256\"", StringComparison.Ordinal));
    }
}

using System.Security.Cryptography;
using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class BaseImplicitHistoryTests
{
    [Fact]
    public void PackageJson_RoundTripsVersionedBaseImplicitEvidenceAndDistinctProvenance()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        const string historicalSourceId = "repoe-historical-base-implicit";
        package = package with
        {
            Manifest = package.Manifest with
            {
                Sources =
                [
                    ..package.Manifest.Sources,
                    new GameDataPackageSource
                    {
                        SourceId = historicalSourceId,
                        RetrievedAtUtc = package.Manifest.CreatedAtUtc,
                        SourceVersion = "c50acab2ed660a70511e7f91ee09db4e632089e4",
                        DataVersion = "3.28.0.13",
                        SourceUri = "https://github.com/repoe-fork/repoe",
                    },
                ],
            },
            BaseImplicitHistory = new BaseImplicitHistoryCatalog
            {
                SourceSnapshots =
                [
                    Source("current", BaseImplicitSnapshotRole.CurrentCandidate, "repoe", "34a9bd548eba7c3b62ab1d1f19a99ae8b12f1564", "3.29.1.2.2"),
                    Source("old", BaseImplicitSnapshotRole.HistoricalObserved, historicalSourceId, "c50acab2ed660a70511e7f91ee09db4e632089e4", "3.28.0.13"),
                ],
                Observations =
                [
                    Observation("item-base.gold-ring", "current"),
                    Observation("item-base.gold-ring", "old"),
                ],
            },
        };

        var json = GameDataPackageJson.Serialize(package);
        var roundTripped = Assert.IsType<GameDataPackage>(GameDataPackageJson.Deserialize(json));

        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
        Assert.NotNull(roundTripped.BaseImplicitHistory);
        Assert.Collection(
            roundTripped.BaseImplicitHistory.SourceSnapshots,
            current => Assert.Equal(BaseImplicitSnapshotRole.CurrentCandidate, current.Role),
            historical =>
            {
                Assert.Equal(BaseImplicitSnapshotRole.HistoricalObserved, historical.Role);
                Assert.Equal("c50acab2ed660a70511e7f91ee09db4e632089e4", historical.CommitSha);
                Assert.Equal("3.28.0.13", historical.DataVersion);
            });
    }

    [Fact]
    public void PackageJson_OldPackageWithoutHistoryMeansEvidenceUnavailableAndStillValid()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var roundTripped = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(GameDataPackageJson.Serialize(package)));

        Assert.Null(roundTripped.BaseImplicitHistory);
        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
    }

    private static BaseImplicitSourceSnapshot Source(
        string id,
        BaseImplicitSnapshotRole role,
        string manifestSourceId,
        string commit,
        string version) => new()
    {
        Id = id,
        Role = role,
        ManifestSourceId = manifestSourceId,
        RepositoryUri = "https://github.com/repoe-fork/repoe",
        CommitSha = commit,
        DataVersion = version,
        Files = [new() { LogicalRole = "baseItems", PackageInputLabel = $"{id}-base_items.json" }],
    };

    private static BaseImplicitObservation Observation(string baseId, string sourceId) => new()
    {
        CanonicalBaseId = baseId,
        SourceSnapshotId = sourceId,
        ImplicitSetMechanicalSignature = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sourceId))).ToLowerInvariant(),
    };
}

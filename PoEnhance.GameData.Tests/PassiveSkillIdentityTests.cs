namespace PoEnhance.GameData.Tests;

public sealed class PassiveSkillIdentityTests
{
    [Fact]
    public void PackageRoundTripAndCatalogPreserveOneToManyCanonicalNames()
    {
        var package = new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test",
                CreatedAtUtc = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero),
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "repoe",
                        RetrievedAtUtc = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero),
                        SourceVersion = "current",
                    },
                ],
            },
            PassiveSkills =
            [
                Identity("Duplicate Passive", 17),
                Identity("Duplicate Passive", 18),
                Identity("Lethality", 41119),
            ],
        };

        var roundTripped = GameDataPackageJson.Deserialize(GameDataPackageJson.Serialize(package));
        var catalog = GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(roundTripped));

        Assert.Equal(
            [17, 18],
            catalog.FindPassiveSkillsByExactName("Duplicate Passive")
                .Select(identity => identity.PassiveHash));
        Assert.Empty(catalog.FindPassiveSkillsByExactName("duplicate passive"));
        Assert.Equal(
            41119,
            Assert.Single(catalog.FindPassiveSkillsByExactName("Lethality")).PassiveHash);
    }

    private static PassiveSkillIdentity Identity(string name, int hash) => new()
    {
        CanonicalName = name,
        PassiveHash = hash,
        InternalId = $"passive-{hash}",
        Sources =
        [
            new GameDataSourceReference
            {
                SourceId = "repoe",
                ExternalId = hash.ToString(),
            },
        ],
    };
}

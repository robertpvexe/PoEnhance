using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class UniqueItemCatalogTests
{
    [Fact]
    public void Validate_SchemaTwoWithoutCatalog_IsInvalid()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        package = package with
        {
            Manifest = package.Manifest with { SchemaVersion = 2 },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.PackageUniqueItemsRequired);
    }

    [Fact]
    public void JsonRoundTrip_ValidCatalog_PreservesVersionBlockAndProvenance()
    {
        var package = CreatePackage();

        var roundTripped = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(GameDataPackageJson.Serialize(package)));

        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
        var identity = Assert.Single(Assert.IsType<UniqueItemCatalog>(roundTripped.UniqueItems).Items);
        Assert.Equal(UniqueItemKind.Replica, identity.Kind);
        var version = Assert.Single(identity.Versions);
        Assert.Equal(UniqueItemVersionRole.Historical, version.Role);
        var block = Assert.Single(version.ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["mod.prefix.maximum-life.t5"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["base_maximum_life"], block.MechanicalMapping.StatIds);
        Assert.Equal(["pob:test"], block.SourceObservationIds);
    }

    [Fact]
    public void Validate_UnknownMechanicalReference_FailsClosed()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items);
        var version = Assert.Single(identity.Versions);
        var block = Assert.Single(version.ModifierBlocks);
        package = package with
        {
            UniqueItems = catalog with
            {
                Items =
                [
                    identity with
                    {
                        Versions =
                        [
                            version with
                            {
                                ModifierBlocks =
                                [
                                    block with
                                    {
                                        MechanicalMapping = block.MechanicalMapping with
                                        {
                                            ModifierIds = ["mod.does-not-exist"],
                                        },
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.UniqueCatalogBlockInvalid);
    }

    private static GameDataPackage CreatePackage()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        const string sourceObservationId = "pob:test";
        return package with
        {
            Manifest = package.Manifest with
            {
                SchemaVersion = 2,
                Sources =
                [
                    .. package.Manifest.Sources,
                    new GameDataPackageSource
                    {
                        SourceId = "path-of-building",
                        RetrievedAtUtc = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
                        SourceVersion = "v2.67.2",
                        SourceUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                    },
                ],
            },
            UniqueItems = new UniqueItemCatalog
            {
                SourceObservations =
                [
                    new UniqueCatalogSourceObservation
                    {
                        Id = sourceObservationId,
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/test.lua",
                        IsGenerated = true,
                        ObservedKind = UniqueItemKind.Replica,
                        RawEntrySha256 = new string('a', 64),
                    },
                ],
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:test",
                        CanonicalName = "Replica Test Item",
                        Kind = UniqueItemKind.Replica,
                        BaseTypeEvidence = ["Gold Ring"],
                        SourceObservationIds = [sourceObservationId],
                        Versions =
                        [
                            new UniqueItemVersionObservation
                            {
                                Id = "unique-version:test",
                                Label = "Pre 3.29.0",
                                Role = UniqueItemVersionRole.Historical,
                                BaseType = "Gold Ring",
                                SourceObservationIds = [sourceObservationId],
                                ModifierBlocks =
                                [
                                    new UniqueModifierBlock
                                    {
                                        Id = "unique-block:test",
                                        Kind = UniqueModifierBlockKind.Unique,
                                        Lines = ["+(50-59) to maximum Life"],
                                        CanonicalSignatures = ["+<number> to maximum Life"],
                                        SourceObservationIds = [sourceObservationId],
                                        MechanicalMapping = new UniqueModifierMechanicalMapping
                                        {
                                            Status = UniqueModifierMechanicalMappingStatus.Exact,
                                            ModifierIds = ["mod.prefix.maximum-life.t5"],
                                            StatIds = ["base_maximum_life"],
                                        },
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };
    }
}

using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class GameDataPackageManifestValidatorTests
{
    [Fact]
    public void Validate_DevelopmentManifest_ReturnsValidResult()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest();

        var result = GameDataPackageManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingDataVersion_ReturnsUnderstandableError()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest() with
        {
            DataVersion = " ",
        };

        var result = GameDataPackageManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains("DataVersion is required.", result.Errors);
    }

    [Fact]
    public void Validate_DuplicateSourceId_ReturnsUnderstandableError()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest() with
        {
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "repoe",
                    RetrievedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 5, 0, TimeSpan.Zero),
                },
                new GameDataPackageSource
                {
                    SourceId = "RePoE",
                    RetrievedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 10, 0, TimeSpan.Zero),
                },
            ],
        };

        var result = GameDataPackageManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains("SourceId 'RePoE' is duplicated.", result.Errors);
    }

    [Fact]
    public void Validate_NonUtcTimestamps_ReturnsUnderstandableErrors()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest() with
        {
            CreatedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.FromHours(1)),
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "repoe",
                    RetrievedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 5, 0, TimeSpan.FromHours(1)),
                },
            ],
        };

        var result = GameDataPackageManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains("CreatedAtUtc must be a UTC timestamp.", result.Errors);
        Assert.Contains("Sources[0].RetrievedAtUtc must be a UTC timestamp.", result.Errors);
    }

    [Fact]
    public void Validate_SchemaVersionLessThanOne_ReturnsUnderstandableError()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest() with
        {
            SchemaVersion = 0,
        };

        var result = GameDataPackageManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains("SchemaVersion must be 1 or greater.", result.Errors);
    }

    [Fact]
    public void Validate_MissingSourceId_ReturnsUnderstandableError()
    {
        var manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest() with
        {
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "",
                    RetrievedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 5, 0, TimeSpan.Zero),
                },
            ],
        };

        var result = GameDataPackageManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains("Sources[0].SourceId is required.", result.Errors);
    }
}

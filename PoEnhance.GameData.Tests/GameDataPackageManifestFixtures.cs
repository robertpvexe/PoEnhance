using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

internal static class GameDataPackageManifestFixtures
{
    public static GameDataPackageManifest CreateDevelopmentManifest()
    {
        return new GameDataPackageManifest
        {
            SchemaVersion = 1,
            DataVersion = "dev-2026-01-15",
            CreatedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            League = "Mercenaries",
            Patch = "3.26.0",
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "repoe",
                    RetrievedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 5, 0, TimeSpan.Zero),
                    SourceVersion = "repoe-dev-snapshot",
                    SourceUri = "https://github.com/brather1ng/RePoE",
                },
                new GameDataPackageSource
                {
                    SourceId = "poedb",
                    RetrievedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 10, 0, TimeSpan.Zero),
                    SourceVersion = "poedb-dev-snapshot",
                    SourceUri = "https://poedb.tw",
                },
            ],
        };
    }
}

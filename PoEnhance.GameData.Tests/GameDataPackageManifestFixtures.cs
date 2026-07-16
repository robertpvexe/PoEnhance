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
            ReviewedItemPropertySemantics = new GameDataPackageReviewedItemPropertySemanticInput
            {
                SourceId = "poenhance.item-property-semantics",
                Label = "item-property-semantics.json",
                DisplayPath = "data/semantics/item-property-semantics.json",
                SizeBytes = 4_096,
                Sha256 = "34662c1f373de8fe1b19efd500b97020985ff3c07044c941c837aa200a3f16d8",
                SchemaVersion = 1,
                ReviewVersion = "weapon-dps-v1",
            },
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "repoe",
                    RetrievedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 5, 0, TimeSpan.Zero),
                    SourceVersion = "c50acab2ed660a70511e7f91ee09db4e632089e4",
                    SourceUri = "https://github.com/repoe-fork/repoe",
                    SourceBranch = "master",
                    SourceRoot = "/sources/repoe-fork",
                    SourceDataRoot = "/sources/active-poe1",
                    InputFiles =
                    [
                        new GameDataPackageInputFileFingerprint
                        {
                            Label = "base_items.json",
                            RelativePath = "base_items.json",
                            SizeBytes = 1_024,
                            Sha256 = "96669bd7d4d7552e8cb2f15ee5fd0173580c7b14ca17583f55645b275a4d6ad1",
                        },
                        new GameDataPackageInputFileFingerprint
                        {
                            Label = "mods.json",
                            RelativePath = "mods.json",
                            SizeBytes = 2_048,
                            Sha256 = "785bf97bc7c0d4485e38355bc880887177904aae14bd38271d788b1e5611b26d",
                        },
                    ],
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

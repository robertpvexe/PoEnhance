using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

internal static class RePoeImportTestFixtures
{
    public static string ReducedBaseItemsPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "RePoE", "base_items.reduced.json");

    public static string ReducedStatsPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "RePoE", "stats.reduced.json");

    public static string ReducedModsPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "RePoE", "mods.reduced.json");

    public static string ReducedStatTranslationsPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "RePoE", "stat_translations.reduced.json");

    public static string ReviewedItemPropertySemanticsPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "Semantics", "item-property-semantics.json");

    public static GameDataPackageManifest CreateManifestWithRePoeSource()
    {
        return new GameDataPackageManifest
        {
            SchemaVersion = 1,
            DataVersion = "dev-repoe-base-items",
            CreatedAtUtc = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero),
            League = "Mercenaries",
            Patch = "3.26.0",
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = RePoeBaseItemImporter.SourceId,
                    RetrievedAtUtc = new DateTimeOffset(2026, 7, 9, 12, 5, 0, TimeSpan.Zero),
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
                            SizeBytes = 1,
                            Sha256 = "96669bd7d4d7552e8cb2f15ee5fd0173580c7b14ca17583f55645b275a4d6ad1",
                        },
                    ],
                },
            ],
        };
    }
}

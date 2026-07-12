using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

internal static class RePoeImportTestFixtures
{
    public static string ReducedBaseItemsPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "RePoE", "base_items.reduced.json");

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
                    SourceVersion = "8023a1d696dbddc836c05ac3fcedd072da1767d2",
                    SourceUri = "https://github.com/brather1ng/RePoE",
                },
            ],
        };
    }
}

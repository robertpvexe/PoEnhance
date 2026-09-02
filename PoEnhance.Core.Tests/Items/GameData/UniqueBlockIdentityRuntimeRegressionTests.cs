using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class UniqueBlockIdentityRuntimeRegressionTests
{
    [Fact]
    public async Task ActiveCatalog_RotmotherMutiny_RetainsDistinctAllResistanceSourceBlocks()
    {
        var catalog = await LoadActiveCatalogAsync();
        var identity = Assert.Single(
            catalog.FindUniqueItemsByExactName("Rotmother's Mutiny"),
            item => item.Kind == UniqueItemKind.Ordinary);
        var version = Assert.Single(identity.Versions, item => item.Label == "Cold");
        var resistanceBlocks = version.ModifierBlocks
            .Where(block => block.Lines.Any(line => line.Contains("Elemental Resistances", StringComparison.Ordinal)))
            .ToArray();

        Assert.Equal(2, resistanceBlocks.Length);
        Assert.Equal(2, resistanceBlocks.Select(block => block.Id).Distinct(StringComparer.Ordinal).Count());

        var lowBlock = Assert.Single(resistanceBlocks, block => block.Lines[0].Contains("(8-10)", StringComparison.Ordinal));
        var highBlock = Assert.Single(resistanceBlocks, block => block.Lines[0].Contains("(20-25)", StringComparison.Ordinal));
        Assert.Contains("AllResistancesUniqueAmulet87", highBlock.MechanicalMapping.ModifierIds);
        Assert.Contains("AllResistancesImplicitAmulet1", lowBlock.MechanicalMapping.ModifierIds);
        Assert.DoesNotContain(
            highBlock.MechanicalMapping.ModifierIds,
            id => id.Equals("AllResistancesImplicitAmulet1", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<GameDataCatalog> LoadActiveCatalogAsync()
    {
        var path = FindRepoFile("artifacts", "poenhance-game-data.json");
        var loaded = await GameDataPackageLoader.LoadFromFileAsync(path);
        Assert.True(loaded.IsSuccess);
        return GameDataCatalog.FromPackage(loaded.Package!);
    }

    private static string FindRepoFile(params string[] parts)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", Path.Combine(parts)));
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts)));
        }

        Assert.True(File.Exists(path), $"GameData package not found: {path}");
        return path;
    }
}

using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchDraftMapperActionSpeedBoundTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Theory]
    [InlineData(5, "Grand")]
    [InlineData(4, "Lesser")]
    public async Task CreateDraft_IncreasedActionSpeedEldritchImplicit_UsesPositiveMinimum(
        int observed,
        string eldritchTier)
    {
        // Synthetic clipboard matching Tempest Road shape (real item absent from corpus).
        // Official Trade: implicit.stat_2878959938 = "#% increased Action Speed".
        var clipboard = string.Format(
            """
            Item Class: Boots
            Rarity: Rare
            Tempest Road
            Wyvernscale Boots
            --------
            Armour: 121
            Evasion Rating: 121
            --------
            Requirements:
            Level: 64
            Str: 62
            Dex: 62
            --------
            Item Level: 84
            --------
            Searing Exarch Item
            --------
            {{ Searing Exarch Implicit Modifier ({0}) }}
            {1}% increased Action Speed
            --------
            20% increased Movement Speed
            +40 to maximum Life
            """,
            eldritchTier,
            observed);
        var item = parser.Parse(clipboard);
        var catalog = await LoadRepoCatalogAsync();
        var baseResolution = new ParsedItemBaseResolver().Resolve(item, catalog);
        var resolutions = new ParsedItemModifierCandidateResolver()
            .Resolve(item, catalog, baseResolution);

        var result = mapper.CreateDraft(item, baseResolution, resolutions, catalog);

        Assert.True(result.IsSuccess);
        var actionSpeed = Assert.Single(
            result.Draft!.ModifierFilters,
            component => component.OriginalText.Contains(
                "increased Action Speed",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ParsedModifierKind.Implicit, actionSpeed.ResolvedSourceKind);
        Assert.Equal(ModifierBoundDirection.Minimum, actionSpeed.DefaultBoundDirection);
        Assert.Equal(observed, actionSpeed.RequestedMinimum);
        Assert.Null(actionSpeed.RequestedMaximum);
        Assert.Equal([observed], actionSpeed.CanonicalNumericValues);
        Assert.Equal("<number>% increased Action Speed", actionSpeed.ProviderCanonicalSignature);
        Assert.Contains(
            actionSpeed.ValueBoundTranslationHandlers,
            handlers => handlers.Any(handler =>
                handler.Equals("negate", StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task<GameDataCatalog> LoadRepoCatalogAsync()
    {
        var load = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(load.Package!);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(string.Join("/", relativeParts));
    }
}

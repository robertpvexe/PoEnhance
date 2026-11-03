using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

/// <summary>
/// Stage 03.11C: live Pain Circle used Crafted Fire+Chaos Rank 3 (+15(13-15)%). JunMaster and
/// JunMaster2 share that range as Disabled crafts; prior resolver left Status=Unknown so Group A
/// projection never ran. Fixtures use authentic RePoE crafted ranks (no item-name gates).
/// </summary>
public sealed class Stage0311CCraftedFireChaosDiagnosticTests
{
    [Theory]
    [InlineData(1, "+9(8-9)% to Fire and Chaos Resistances")]
    [InlineData(2, "+11(10-12)% to Fire and Chaos Resistances")]
    [InlineData(3, "+15(13-15)% to Fire and Chaos Resistances")]
    [InlineData(2, "+10(9-10)% to Fire and Chaos Resistances")]
    public async Task CraftedFireAndChaos_AllRanks_ProjectIntoIndependentLogicalRows(
        int rank,
        string line)
    {
        var catalog = await LoadCatalogAsync();
        var clipboard = $$"""
            Item Class: Rings
            Rarity: Rare
            Synthetic Pain
            Two-Stone Ring
            --------
            Requirements:
            Level: 60
            --------
            Item Level: 85
            --------
            { Suffix Modifier "of the Whelpling" (Tier: 8) }
            +20(16-20)% to Fire Resistance
            { Master Crafted Suffix Modifier "of Craft" (Rank: {{rank}}) }
            {{line}}
            """;
        var item = new ItemTextParser().Parse(clipboard);
        var baseResolution = new ParsedItemBaseResolver().Resolve(item, catalog);
        var resolutions = new ParsedItemModifierCandidateResolver().Resolve(item, catalog, baseResolution);
        var craftedIndex = item.Modifiers.ToList().FindIndex(modifier => modifier.IsCrafted);
        var craftedRes = resolutions.Single(result => result.ParsedModifierIndex == craftedIndex);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, craftedRes.Status);

        var draftResult = new TradeSearchDraftMapper().CreateDraft(item, baseResolution, resolutions, catalog);
        Assert.True(draftResult.IsSuccess);
        var rows = draftResult.Draft!.ModifierFilters.Where(component => !component.IsBaseImplicit).ToArray();
        Assert.DoesNotContain(rows, component =>
            component.OriginalText.Contains("Fire and Chaos", StringComparison.Ordinal));
        var fire = Assert.Single(rows, component =>
            component.CanonicalSignature == "+<number>% to Fire Resistance");
        var chaos = Assert.Single(rows, component =>
            component.CanonicalSignature == "+<number>% to Chaos Resistance");
        Assert.True(fire.ObservedNumericValues[0] >= 29m);
        Assert.False(chaos.IsCrafted);
        Assert.NotEmpty(chaos.Sources);
        Assert.All(chaos.Sources, source => Assert.True(source.IsCrafted));
        Assert.Single(chaos.Sources.Select(source => source.PhysicalSourceIdentity).Distinct());
        Assert.Contains(":logic:", chaos.PhysicalSourceIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CraftedFireAndChaos_Rank3_PainCircleShape_PseudoFireIncludesOrdinaryAndCraft()
    {
        var catalog = await LoadCatalogAsync();
        var clipboard = """
            Item Class: Rings
            Rarity: Rare
            Synthetic Pain
            Two-Stone Ring
            --------
            Requirements:
            Level: 60
            --------
            Item Level: 85
            --------
            { Suffix Modifier "of the Whelpling" (Tier: 8) }
            +20(16-20)% to Fire Resistance
            { Suffix Modifier "of the Whale" (Tier: 8) }
            +15(12-15)% to Cold Resistance
            { Master Crafted Suffix Modifier "of Craft" (Rank: 3) }
            +15(13-15)% to Fire and Chaos Resistances
            """;
        // Implicit Cold+Lightning added via Two-Stone base resolution when catalog matches.
        var item = new ItemTextParser().Parse(clipboard);
        var baseResolution = new ParsedItemBaseResolver().Resolve(item, catalog);
        var resolutions = new ParsedItemModifierCandidateResolver().Resolve(item, catalog, baseResolution);
        var draft = new TradeSearchDraftMapper().CreateDraft(item, baseResolution, resolutions, catalog).Draft!;
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            draft.ModifierFilters,
            "base_fire_damage_resistance_%",
            out var fireTotal));
        Assert.Equal(35m, fireTotal);
        Assert.True(LogicalEffectItemTotalCalculator.TryCompute(
            draft.ModifierFilters,
            "base_chaos_damage_resistance_%",
            out var chaosTotal));
        Assert.Equal(15m, chaosTotal);
    }

    private static async Task<GameDataCatalog> LoadCatalogAsync()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "artifacts", "poenhance-game-data.json");
            if (!File.Exists(candidate)) continue;
            var load = await GameDataPackageLoader.LoadFromFileAsync(candidate);
            Assert.True(load.IsSuccess);
            return GameDataCatalog.FromPackage(load.Package!);
        }
        throw new FileNotFoundException("poenhance-game-data.json");
    }
}

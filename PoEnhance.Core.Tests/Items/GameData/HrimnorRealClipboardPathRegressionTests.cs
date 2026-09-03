using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// Real clipboard-shape regression for Hrimnor leech after Current/Historical aggregation.
/// Uses CreateDraft with an attached catalog (the production path once Runtime GameData is ready).
/// </summary>
public sealed class HrimnorRealClipboardPathRegressionTests
{
    private const string RealCtrlDClipboard = """
        Item Class: Two Hand Maces
        Rarity: Unique
        Hrimnor's Hymn
        Sledgehammer
        --------
        Two Handed Mace
        Physical Damage: 45-67
        Critical Strike Chance: 5.00%
        Attacks per Second: 1.30
        Weapon Range: 1.3 metres
        --------
        Requirements:
        Level: 17
        Str: 62
        --------
        Item Level: 70
        --------
        { Implicit Modifier }
        45% increased Stun Duration on Enemies
        --------
        { Unique Modifier — Damage, Physical, Attack }
        150(140-200)% increased Physical Damage
        { Unique Modifier — Life, Physical, Attack }
        1% of Physical Attack Damage Leeched as Life
        { Unique Modifier — Attribute }
        +10 to Strength
        { Unique Modifier }
        15% reduced Enemy Stun Threshold
        { Unique Modifier }
        45(40-50)% increased Stun Duration on Enemies
        """;

    [Fact]
    public async Task CreateDraft_WithCatalog_PreservesHrimnorLeechCurrentProvenance()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        var package = Assert.IsType<GameDataPackage>(load.Package);
        var catalog = GameDataCatalog.FromPackage(package);
        var parsed = new ItemTextParser().Parse(RealCtrlDClipboard);

        var draft = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);

        Assert.True(draft.IsSuccess);
        Assert.NotNull(draft.Draft!.UniqueItemResolution);
        var leech = Assert.Single(
            draft.Draft.ModifierFilters,
            component => component.RawCopiedText.Contains("Leeched as Life", StringComparison.OrdinalIgnoreCase));
        Assert.True(leech.HasExactUniqueSourceProvenance);
        Assert.True(leech.IsEquivalentSourceSet);
        Assert.Contains(
            "local_life_leech_from_physical_damage_permyriad",
            leech.ResolvedStatIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            leech.UniqueAggregationDiagnosticCode);
    }
}

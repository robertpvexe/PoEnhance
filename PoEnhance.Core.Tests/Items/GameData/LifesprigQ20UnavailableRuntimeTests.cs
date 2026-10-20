using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class LifesprigQ20UnavailableRuntimeTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public async Task Lifesprig_CurrentUnique_PhysAndTotalDpsAreDisplayOnlyUnavailable()
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = ReplayReadyCaptureFixtures.ReadClipboard(
            ReplayReadyCaptureFixtures.PreferCaptureFileName("20260924-093804-947-Lifesprig.json"));
        var item = parser.Parse(raw);
        Assert.Equal("Lifesprig", item.DisplayName, ignoreCase: true);

        Assert.Contains(
            catalog.FindUniqueItemsByExactName("Lifesprig")
                .SelectMany(identity => identity.Versions),
            version => version.Role == UniqueItemVersionRole.Current);

        var baseResolution = baseResolver.Resolve(item, catalog);
        var resolutions = modifierResolver.Resolve(item, catalog, baseResolution);
        var result = mapper.CreateDraft(item, baseResolution, resolutions, catalog);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var draft = Assert.IsType<TradeSearchDraft>(result.Draft);

        var physical = Assert.Single(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        var total = Assert.Single(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.TotalDps);
        var aps = Assert.Single(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.AttacksPerSecond);
        var crit = Assert.Single(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.CriticalStrikeChance);

        Assert.Equal(10.5m, physical.ObservedValue);
        Assert.Equal(10.5m, total.ObservedValue);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.Unavailable,
            physical.QualityNormalizationStatus);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.Unavailable,
            total.QualityNormalizationStatus);
        Assert.Null(physical.RequestedMinimum);
        Assert.Null(total.RequestedMinimum);
        Assert.Contains(
            "exact sourced Physical Damage",
            physical.DerivationUnsupportedReason,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.NotApplicable,
            aps.QualityNormalizationStatus);
        Assert.Equal(aps.ObservedValue, aps.RequestedMinimum);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.NotApplicable,
            crit.QualityNormalizationStatus);
        Assert.Equal(crit.ObservedValue, crit.RequestedMinimum);
    }

    private static async Task<GameDataCatalog> LoadActiveCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(load.Package!);
    }
}

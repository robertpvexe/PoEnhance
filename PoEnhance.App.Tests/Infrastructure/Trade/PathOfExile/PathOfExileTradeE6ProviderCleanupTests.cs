using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeE6ProviderCleanupTests
{
    [Fact]
    public void BeaconFixture_SourceVariantIdentityRemainsExplicitlyUnsupportedOutsideE6()
    {
        var draft = new TradeSearchDraft
        {
            ItemClass = "Boots",
            Rarity = "Unique",
            DisplayName = "Beacon of Madness",
            ParsedBaseType = "Two-Toned Boots (Armour/Energy Shield)",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "Metadata/Items/Armours/Boots/TwoTonedBootsArmourEnergyShield",
                ResolvedBaseName = "Two-Toned Boots (Armour/Energy Shield)",
            },
        };
        var catalog = new PathOfExileTradeItemCatalog(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "armour",
                GroupLabel = "Armour",
                Name = "Beacon of Madness",
                Type = "Two-Toned Boots",
                IsUnique = true,
            },
        ]);

        var result = new PathOfExileTradeItemIdentityMapper().Map(draft, catalog);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Identity);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueIdentity,
            diagnostic.Code);
    }
}
